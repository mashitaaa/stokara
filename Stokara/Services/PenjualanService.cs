using Stokara.Infrastructure;
using Stokara.Models;
using Stokara.Repositories;

namespace Stokara.Services;

public sealed class SaleRequest
{
    /// <summary>PENJUALAN atau PENYALURAN_SUBSIDI (A-10).</summary>
    public string Jenis { get; set; } = "PENJUALAN";
    public int? AnggotaId { get; set; }
    public int? PenerimaId { get; set; }
    public List<(int BarangId, decimal Jumlah)> Items { get; set; } = new();
    public decimal UangDiterima { get; set; }
    /// <summary>Hanya untuk data contoh (DemoDataSeeder).</summary>
    internal DateTime? Tanggal { get; set; }
}

public sealed class SaleResult
{
    public int PenjualanId { get; set; }
    public Receipt Receipt { get; set; }
}

/// <summary>
/// Penjualan, penyaluran subsidi, pembatalan, dan tutup kasir (FR-17-24).
/// Semua aturan (stok, harga, HET, status penerima, kuota, hutang penitip) diperiksa ulang di sini,
/// di dalam satu transaksi database, bukan di layar.
/// </summary>
public sealed class PenjualanService
{
    private readonly Db _db;
    private readonly BarangRepository _barang;
    private readonly MitraRepository _mitra;
    private readonly KuotaRepository _kuota;
    private readonly KonsinyasiRepository _konsinyasi;
    private readonly TransaksiRepository _trx;
    private readonly AktivitasRepository _aktivitas;
    private readonly SettingsService _settings;

    public PenjualanService(Db db, BarangRepository barang, MitraRepository mitra, KuotaRepository kuota,
        KonsinyasiRepository konsinyasi, TransaksiRepository trx, AktivitasRepository aktivitas, SettingsService settings)
    {
        _db = db;
        _barang = barang;
        _mitra = mitra;
        _kuota = kuota;
        _konsinyasi = konsinyasi;
        _trx = trx;
        _aktivitas = aktivitas;
        _settings = settings;
    }

    public async Task<SaleResult> SaveAsync(SaleRequest req)
    {
        Session.Require("melayani penjualan", Roles.Manajer, Roles.Kasir);
        var subsidi = req.Jenis == "PENYALURAN_SUBSIDI";
        if (!subsidi && req.Jenis != "PENJUALAN") throw new BusinessException("Jenis transaksi tidak dikenal.");
        if (req.Items.Count == 0) throw new BusinessException("Keranjang masih kosong.");
        if (req.Items.Any(i => i.Jumlah <= 0)) throw new BusinessException("Jumlah setiap barang harus lebih dari 0.");
        if (subsidi && !req.PenerimaId.HasValue) throw new BusinessException("Pilih penerima subsidi terlebih dahulu.");

        // Gabungkan baris barang yang sama.
        var items = req.Items.GroupBy(i => i.BarangId).Select(g => (BarangId: g.Key, Jumlah: g.Sum(x => x.Jumlah))).ToList();
        var tanggal = req.Tanggal ?? DateTime.Now;
        var periode = _settings.PeriodeKuota(tanggal);

        return await _db.InTransactionAsync(async (conn, tx) =>
        {
            if (await _trx.IsDayClosedAsync(tanggal, conn, tx))
                throw new BusinessException($"Kasir tanggal {Fmt.Tgl(tanggal)} sudah ditutup. Transaksi baru tidak dapat disimpan.");

            Anggota anggota = null;
            PenerimaSubsidi penerima = null;
            if (subsidi)
            {
                penerima = await _mitra.GetPenerimaAsync(req.PenerimaId.Value, conn, tx)
                           ?? throw new BusinessException("Penerima tidak terdaftar. Barang subsidi tidak dapat disalurkan.");
                if (penerima.Status != "AKTIF")
                    throw new BusinessException($"{penerima.Nama} berstatus nonaktif sehingga tidak dapat menerima barang subsidi.");
            }
            else if (req.AnggotaId.HasValue)
            {
                anggota = await _mitra.GetAnggotaAsync(req.AnggotaId.Value, conn, tx)
                          ?? throw new BusinessException("Anggota tidak ditemukan.");
                // BR-10: harga anggota hanya untuk anggota aktif.
                if (anggota.Status != "AKTIF")
                    throw new BusinessException($"{anggota.Nama} bukan anggota aktif. Layani sebagai pelanggan Umum.");
            }

            // Baca barang dan hitung harga dari database, bukan dari layar (A-01).
            var lines = new List<(Barang B, decimal Jumlah, decimal Harga)>();
            foreach (var (barangId, jumlah) in items)
            {
                var b = await _barang.GetForUpdateAsync(conn, tx, barangId) ?? throw new BusinessException("Barang tidak ditemukan.");
                if (!b.IsActive) throw new BusinessException($"{b.Nama} sudah dinonaktifkan.");
                if (subsidi && !b.IsSubsidi)
                    throw new BusinessException($"{b.Nama} bukan barang subsidi. Layani lewat mode Penjualan.");
                if (!subsidi && b.IsSubsidi)
                    throw new BusinessException($"{b.Nama} adalah barang subsidi. Gunakan mode Penyaluran Subsidi.");
                var harga = b.HargaUntuk(anggota != null);
                if (harga <= 0) throw new BusinessException($"Harga {b.Nama} belum ditetapkan. Hubungi Manajer.");
                if (b.IsSubsidi && (!b.HET.HasValue || harga > b.HET.Value))
                    throw new BusinessException($"Harga {b.Nama} ({Fmt.Rp(harga)}) melebihi HET {Fmt.Rp(b.HET ?? 0)}. " +
                                                "Penyaluran diblokir sampai Manajer menyesuaikan harga.");
                if (b.StokSaatIni < jumlah)
                    throw new BusinessException($"Stok {b.Nama} tidak cukup: tersedia {Fmt.Num(b.StokSaatIni)} {b.SatuanNama}.");
                lines.Add((b, jumlah, harga));
            }

            var total = lines.Sum(l => l.Jumlah * l.Harga);
            if (req.UangDiterima < total)
                throw new BusinessException($"Uang diterima kurang dari total belanja {Fmt.Rp(total)}.");
            var kembalian = req.UangDiterima - total;

            var nomor = await _trx.NextNumberAsync(conn, tx, subsidi ? "SB" : "PJ", tanggal);
            var penjualanId = await _trx.InsertPenjualanAsync(conn, tx, nomor, tanggal, req.Jenis,
                anggota != null ? "ANGGOTA" : "UMUM", anggota?.AnggotaId, penerima?.PenerimaId, total, req.UangDiterima, kembalian);

            foreach (var (b, jumlah, harga) in lines)
            {
                // Kuota (BR-09) - diperbarui atomik, gagal bila melebihi sisa.
                if (subsidi)
                {
                    var kuota = await _kuota.GetKuotaAsync(conn, tx, b.BarangId, penerima.JenisPenerima, periode)
                        ?? throw new BusinessException(
                            $"Kuota {b.Nama} untuk {Fmt.JenisPenerimaLabel(penerima.JenisPenerima)} periode {periode} belum ditetapkan.");
                    if (!await _kuota.TryConsumeAsync(conn, tx, penerima.PenerimaId, b.BarangId, periode, jumlah, kuota))
                    {
                        var terpakai = await _kuota.GetTerpakaiAsync(conn, tx, penerima.PenerimaId, b.BarangId, periode);
                        var sisa = Math.Max(0, kuota - terpakai);
                        throw new BusinessException(sisa <= 0
                            ? $"Kuota {b.Nama} untuk {penerima.Nama} periode {periode} sudah habis."
                            : $"Jumlah melebihi sisa kuota {penerima.Nama}: sisa {Fmt.Num(sisa)} {b.SatuanNama}.");
                    }
                }

                var hargaPokok = b.IsTitipan ? (b.HargaTitip ?? 0) : b.HargaPokokRata;
                var detailId = await _trx.InsertPenjualanDetailAsync(conn, tx, penjualanId, b.BarangId, jumlah, harga, hargaPokok,
                    subsidi ? periode : null);

                // Stok (BR-02) - atomik, gagal bila stok sudah diambil transaksi lain.
                var saldo = await _barang.DecreaseAsync(conn, tx, b.BarangId, jumlah)
                            ?? throw new BusinessException($"Stok {b.Nama} baru saja berubah dan tidak cukup. Periksa lagi.");
                await _barang.InsertMutasiAsync(conn, tx, b.BarangId, tanggal, subsidi ? "KELUAR_SUBSIDI" : "KELUAR_JUAL",
                    "PENJUALAN", penjualanId, nomor, 0, jumlah, saldo, penerima?.Nama ?? anggota?.Nama);

                // Hutang penitip (FR-24, BR-12).
                if (b.IsTitipan)
                    await _konsinyasi.InsertHutangAsync(conn, tx, b.PenitipId.Value, detailId, b.BarangId, jumlah, b.HargaTitip ?? 0, harga);
            }

            await _aktivitas.InsertAsync(Session.UserId, subsidi ? "PENYALURAN" : "PENJUALAN", "Penjualan", nomor,
                $"{lines.Count} item, total {total:N0}", conn, tx);

            return new SaleResult
            {
                PenjualanId = penjualanId,
                Receipt = new Receipt
                {
                    Judul = subsidi ? "BUKTI PENYALURAN SUBSIDI" : "STRUK PENJUALAN",
                    NomorDokumen = nomor,
                    Tanggal = tanggal,
                    Kasir = Session.Current.Nama,
                    Pihak = penerima != null ? $"{penerima.Nama} ({Fmt.MaskNik(penerima.NIK)})"
                          : anggota != null ? $"{anggota.Nama} ({anggota.NomorAnggota})" : "Umum",
                    Items = lines.Select(l => new TransaksiDetailRow
                    {
                        BarangNama = l.B.Nama, Satuan = l.B.SatuanNama, Jumlah = l.Jumlah, Harga = l.Harga,
                        Subtotal = l.Jumlah * l.Harga, Keterangan = l.B.IsSubsidi ? "SUBSIDI" : l.B.IsTitipan ? "TITIPAN" : ""
                    }).ToList(),
                    Total = total,
                    UangDiterima = req.UangDiterima,
                    Kembalian = kembalian,
                    Catatan = subsidi ? $"Periode kuota {periode}" : null
                }
            };
        });
    }

    /// <summary>
    /// Void (A-13): hanya Manajer, hanya transaksi hari ini yang harinya belum ditutup,
    /// ditolak bila hutang penitip terkait sudah dibayar. Stok, kuota, dan hutang dipulihkan.
    /// </summary>
    public async Task VoidAsync(int penjualanId, string alasan)
    {
        Session.Require("membatalkan transaksi", Roles.Manajer);
        if (string.IsNullOrWhiteSpace(alasan)) throw new BusinessException("Alasan pembatalan wajib diisi.");

        await _db.InTransactionAsync(async (conn, tx) =>
        {
            var h = await _trx.GetPenjualanForUpdateAsync(conn, tx, penjualanId) ?? throw new BusinessException("Transaksi tidak ditemukan.");
            if (h.Status != "AKTIF") throw new BusinessException("Transaksi ini sudah dibatalkan.");
            if (h.Tanggal.Date != DateTime.Today)
                throw new BusinessException("Hanya transaksi hari ini yang dapat dibatalkan. Koreksi hari lain lewat stok opname.");
            if (await _trx.IsDayClosedAsync(h.Tanggal, conn, tx))
                throw new BusinessException("Kasir hari ini sudah ditutup, transaksi tidak dapat dibatalkan.");

            var lines = await _trx.GetPenjualanLinesAsync(conn, tx, penjualanId);
            if (lines.Any(l => l.HutangId.HasValue && l.HutangDibayar > 0))
                throw new BusinessException("Hutang ke penitip untuk transaksi ini sudah dibayar, sehingga tidak dapat dibatalkan.");

            var now = DateTime.Now;
            foreach (var l in lines)
            {
                var saldo = await _barang.IncreaseAsync(conn, tx, l.BarangId, l.Jumlah) ?? 0;
                await _barang.InsertMutasiAsync(conn, tx, l.BarangId, now, "VOID", "PENJUALAN", penjualanId, h.NomorDokumen,
                    l.Jumlah, 0, saldo, "Batal: " + alasan.Trim());
                if (h.PenerimaId.HasValue && l.PeriodeKuota != null)
                    await _kuota.RestoreAsync(conn, tx, h.PenerimaId.Value, l.BarangId, l.PeriodeKuota, l.Jumlah);
                if (l.HutangId.HasValue)
                    await _konsinyasi.CancelHutangAsync(conn, tx, l.HutangId.Value);
            }
            await _trx.MarkPenjualanVoidAsync(conn, tx, penjualanId, alasan.Trim());
            await _aktivitas.InsertAsync(Session.UserId, "VOID", "Penjualan", h.NomorDokumen, alasan.Trim(), conn, tx);
        });
    }

    public async Task<List<TransaksiRow>> ListAsync(DateTime dari, DateTime sampai, string jenis, string kw)
    {
        Session.Require("melihat riwayat transaksi", Roles.Manajer, Roles.Kasir, Roles.Pengurus);
        // Kasir hanya melihat transaksi miliknya hari ini (matriks 4.1).
        if (Session.Is(Roles.Kasir))
            return await _trx.ListPenjualanAsync(DateTime.Today, DateTime.Today, jenis, kw, Session.UserId);
        return await _trx.ListPenjualanAsync(dari, sampai, jenis, kw, null);
    }

    public Task<List<TransaksiDetailRow>> DetailAsync(int id) => _trx.PenjualanDetailAsync(id);

    public async Task<Receipt> ReceiptAsync(int penjualanId)
    {
        var h = await _trx.GetPenjualanAsync(penjualanId);
        if (h == null) return null;
        return new Receipt
        {
            Judul = (h.Jenis == "PENYALURAN_SUBSIDI" ? "BUKTI PENYALURAN SUBSIDI" : "STRUK PENJUALAN") + " (CETAK ULANG)",
            NomorDokumen = h.NomorDokumen,
            Tanggal = h.Tanggal,
            Kasir = h.Kasir,
            Pihak = h.Pihak,
            Items = await _trx.PenjualanDetailAsync(penjualanId),
            Total = h.Total,
            UangDiterima = h.UangDiterima,
            Kembalian = h.Kembalian,
            Catatan = h.Status == "DIBATALKAN" ? "*** TRANSAKSI DIBATALKAN ***" : null
        };
    }

    // ============================ Tutup kasir (FR-23, A-17) ============================

    public Task<bool> IsTodayClosedAsync() => _trx.IsDayClosedAsync(DateTime.Today);
    public Task<(int Jumlah, decimal Total)> RingkasanHariIniAsync() => _trx.RingkasanHariAsync(DateTime.Today);
    public Task<List<TutupHariRow>> ListTutupHariAsync() => _trx.ListTutupHariAsync();

    public async Task TutupHariAsync(decimal modalAwal, decimal uangFisik, string catatan)
    {
        Session.Require("menutup kasir", Roles.Manajer, Roles.Kasir);
        if (modalAwal < 0 || uangFisik < 0) throw new BusinessException("Nilai uang tidak boleh negatif.");
        var hari = DateTime.Today;
        await _db.InTransactionAsync(async (conn, tx) =>
        {
            if (await _trx.IsDayClosedAsync(hari, conn, tx)) throw new BusinessException("Kasir hari ini sudah ditutup.");
            var (jumlah, total) = await conn.QuerySingleAsync(@"
SELECT COUNT(*) AS Jumlah, ISNULL(SUM(Total), 0) AS Total FROM dbo.Penjualan WITH (HOLDLOCK)
WHERE Status = 'AKTIF' AND Tanggal >= @Dari AND Tanggal < @Sampai",
                r => (r.Int("Jumlah"), r.Dec("Total")), new { Dari = hari, Sampai = hari.AddDays(1) }, tx);
            var seharusnya = modalAwal + total;
            await _trx.InsertTutupHariAsync(conn, tx, hari, jumlah, total, modalAwal, seharusnya, uangFisik,
                string.IsNullOrWhiteSpace(catatan) ? null : catatan.Trim());
            await _aktivitas.InsertAsync(Session.UserId, "TUTUP_KASIR", "TutupHari", hari.ToString("yyyy-MM-dd"),
                $"{jumlah} transaksi, total {total:N0}, selisih {uangFisik - seharusnya:N0}", conn, tx);
        });
    }
}
