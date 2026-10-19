using Stokara.Infrastructure;
using Stokara.Models;
using Stokara.Repositories;

namespace Stokara.Services;

public sealed class PenerimaanRequest
{
    public string Sumber { get; set; }
    public int? PemasokId { get; set; }
    public int? PenitipId { get; set; }
    public string NomorSuratJalan { get; set; }
    public string Catatan { get; set; }
    public List<LineItem> Items { get; set; } = new();
    /// <summary>Hanya untuk data contoh (DemoDataSeeder).</summary>
    internal DateTime? Tanggal { get; set; }
}

public sealed class PengeluaranRequest
{
    public string Jenis { get; set; }
    public int? PemasokId { get; set; }
    public int? PenitipId { get; set; }
    public string Alasan { get; set; }
    public List<LineItem> Items { get; set; } = new();
}

/// <summary>Penerimaan barang (FR-13), pengeluaran non-penjualan (FR-14), dan pembatalannya.</summary>
public sealed class StokService
{
    private readonly Db _db;
    private readonly BarangRepository _barang;
    private readonly TransaksiRepository _trx;
    private readonly AktivitasRepository _aktivitas;

    public StokService(Db db, BarangRepository barang, TransaksiRepository trx, AktivitasRepository aktivitas)
    {
        _db = db;
        _barang = barang;
        _trx = trx;
        _aktivitas = aktivitas;
    }

    public static readonly string[] SumberPenerimaan = { "PEMBELIAN", "SUBSIDI", "KONSINYASI", "STOK_AWAL" };
    public static readonly string[] JenisPengeluaran = { "RETUR_PEMASOK", "RETUR_PENITIP", "RUSAK", "KEDALUWARSA", "HILANG" };

    /// <summary>Filter barang yang boleh dipilih untuk sumber penerimaan tertentu.</summary>
    public static string JenisBarangUntukSumber(string sumber) => sumber switch
    {
        "SUBSIDI" => "SUBSIDI",
        "KONSINYASI" => "TITIPAN",
        "PEMBELIAN" => "MILIK_NONSUBSIDI",
        _ => "MILIK"
    };

    public Task<List<TransaksiRow>> ListPenerimaanAsync(DateTime dari, DateTime sampai, string sumber, string kw)
        => _trx.ListPenerimaanAsync(dari, sampai, sumber, kw);
    public Task<List<TransaksiDetailRow>> PenerimaanDetailAsync(int id) => _trx.PenerimaanDetailAsync(id);
    public Task<List<TransaksiRow>> ListPengeluaranAsync(DateTime dari, DateTime sampai, string jenis) => _trx.ListPengeluaranAsync(dari, sampai, jenis);
    public Task<List<TransaksiDetailRow>> PengeluaranDetailAsync(int id) => _trx.PengeluaranDetailAsync(id);

    /// <summary>
    /// Menyimpan penerimaan: header, detail, stok, HPP rata-rata, dan mutasi dalam satu transaksi (BR-17).
    /// </summary>
    public async Task<string> SavePenerimaanAsync(PenerimaanRequest req)
    {
        Session.Require("mencatat penerimaan barang", Roles.Manajer, Roles.Gudang);
        if (!SumberPenerimaan.Contains(req.Sumber)) throw new BusinessException("Pilih sumber penerimaan.");
        if (req.Sumber == "KONSINYASI" && !req.PenitipId.HasValue) throw new BusinessException("Pilih penitip untuk barang titipan.");
        if ((req.Sumber == "PEMBELIAN" || req.Sumber == "SUBSIDI") && !req.PemasokId.HasValue)
            throw new BusinessException("Pilih pemasok.");
        if (req.Items.Count == 0) throw new BusinessException("Tambahkan minimal satu barang.");
        if (req.Items.Any(i => i.Jumlah <= 0)) throw new BusinessException("Jumlah setiap barang harus lebih dari 0.");
        if (req.Items.Any(i => i.Harga < 0)) throw new BusinessException("Harga tidak boleh negatif.");
        if (req.Items.GroupBy(i => i.BarangId).Any(g => g.Count() > 1))
            throw new BusinessException("Ada barang yang dimasukkan dua kali. Gabungkan jumlahnya dalam satu baris.");

        var tanggal = req.Tanggal ?? DateTime.Now;
        var mutasi = req.Sumber switch
        {
            "PEMBELIAN" => "MASUK_PEMBELIAN",
            "SUBSIDI" => "MASUK_SUBSIDI",
            "KONSINYASI" => "MASUK_KONSINYASI",
            _ => "MASUK_STOK_AWAL"
        };

        var nomor = await _db.InTransactionAsync(async (conn, tx) =>
        {
            var no = await _trx.NextNumberAsync(conn, tx, "PN", tanggal);
            var total = req.Items.Sum(i => i.Jumlah * i.Harga);
            var id = await _trx.InsertPenerimaanAsync(conn, tx, no, tanggal, req.Sumber,
                req.Sumber == "KONSINYASI" ? null : req.PemasokId,
                req.Sumber == "KONSINYASI" ? req.PenitipId : null,
                string.IsNullOrWhiteSpace(req.NomorSuratJalan) ? null : req.NomorSuratJalan.Trim(),
                string.IsNullOrWhiteSpace(req.Catatan) ? null : req.Catatan.Trim(), total);

            foreach (var item in req.Items)
            {
                var b = await _barang.GetForUpdateAsync(conn, tx, item.BarangId)
                        ?? throw new BusinessException("Barang tidak ditemukan.");
                if (!b.IsActive) throw new BusinessException($"{b.Nama} sudah dinonaktifkan.");
                switch (req.Sumber)
                {
                    case "KONSINYASI" when !b.IsTitipan || b.PenitipId != req.PenitipId:
                        throw new BusinessException($"{b.Nama} bukan barang titipan dari penitip yang dipilih.");
                    case "SUBSIDI" when !b.IsSubsidi:
                        throw new BusinessException($"{b.Nama} bukan barang subsidi. Gunakan sumber Pembelian.");
                    case "PEMBELIAN" when b.IsSubsidi || b.IsTitipan:
                        throw new BusinessException($"{b.Nama} harus diterima lewat sumber {(b.IsSubsidi ? "Pasokan Subsidi" : "Konsinyasi")}.");
                    case "STOK_AWAL" when b.IsTitipan:
                        throw new BusinessException($"{b.Nama} adalah titipan; gunakan sumber Konsinyasi.");
                }

                await _trx.InsertPenerimaanDetailAsync(conn, tx, id, b.BarangId, item.Jumlah, item.Harga, item.TanggalKedaluwarsa);
                // Barang titipan tidak memengaruhi HPP koperasi (BR-11).
                var saldo = b.IsTitipan
                    ? await _barang.IncreaseAsync(conn, tx, b.BarangId, item.Jumlah)
                    : await _barang.IncreaseWithHppAsync(conn, tx, b.BarangId, item.Jumlah, item.Harga);
                await _barang.InsertMutasiAsync(conn, tx, b.BarangId, tanggal, mutasi, "PENERIMAAN", id, no,
                    item.Jumlah, 0, saldo ?? 0, null);
            }

            await _aktivitas.InsertAsync(Session.UserId, "PENERIMAAN", "Penerimaan", no,
                $"{Fmt.SumberLabel(req.Sumber)}, {req.Items.Count} item", conn, tx);
            return no;
        });
        return nomor;
    }

    /// <summary>Void penerimaan (A-13): hanya hari ini, dan ditolak bila stok sudah terpakai.</summary>
    public async Task VoidPenerimaanAsync(int penerimaanId, string alasan)
    {
        Session.Require("membatalkan penerimaan", Roles.Manajer);
        if (string.IsNullOrWhiteSpace(alasan)) throw new BusinessException("Alasan pembatalan wajib diisi.");

        await _db.InTransactionAsync(async (conn, tx) =>
        {
            var h = await _trx.GetPenerimaanForUpdateAsync(conn, tx, penerimaanId)
                    ?? throw new BusinessException("Penerimaan tidak ditemukan.");
            if (h.Status != "AKTIF") throw new BusinessException("Penerimaan ini sudah dibatalkan.");
            if (h.Tanggal.Date != DateTime.Today)
                throw new BusinessException("Hanya penerimaan hari ini yang dapat dibatalkan. Untuk hari lain, gunakan stok opname.");

            var now = DateTime.Now;
            foreach (var line in await _trx.GetPenerimaanLinesAsync(conn, tx, penerimaanId))
            {
                var saldo = line.Kepemilikan == "TITIPAN"
                    ? await _barang.DecreaseAsync(conn, tx, line.BarangId, line.Jumlah)
                    : await _barang.DecreaseReverseHppAsync(conn, tx, line.BarangId, line.Jumlah, line.HargaBeli);
                if (saldo == null)
                {
                    var b = await _barang.GetForUpdateAsync(conn, tx, line.BarangId);
                    throw new BusinessException(
                        $"Tidak bisa dibatalkan: stok {b.Nama} tinggal {Fmt.Num(b.StokSaatIni)}, lebih kecil dari jumlah yang diterima ({Fmt.Num(line.Jumlah)}).");
                }
                await _barang.InsertMutasiAsync(conn, tx, line.BarangId, now, "VOID", "PENERIMAAN", penerimaanId, h.NomorDokumen,
                    0, line.Jumlah, saldo.Value, "Batal: " + alasan.Trim());
            }
            await _trx.MarkPenerimaanVoidAsync(conn, tx, penerimaanId, alasan.Trim());
            await _aktivitas.InsertAsync(Session.UserId, "VOID", "Penerimaan", h.NomorDokumen, alasan.Trim(), conn, tx);
        });
    }

    /// <summary>Retur ke pemasok/penitip, rusak, kedaluwarsa, hilang (FR-14). Alasan wajib.</summary>
    public async Task<string> SavePengeluaranAsync(PengeluaranRequest req)
    {
        Session.Require("mencatat retur/barang rusak", Roles.Manajer, Roles.Gudang);
        if (!JenisPengeluaran.Contains(req.Jenis)) throw new BusinessException("Pilih jenis pengeluaran.");
        if (string.IsNullOrWhiteSpace(req.Alasan)) throw new BusinessException("Alasan wajib diisi.");
        if (req.Jenis == "RETUR_PEMASOK" && !req.PemasokId.HasValue) throw new BusinessException("Pilih pemasok tujuan retur.");
        if (req.Jenis == "RETUR_PENITIP" && !req.PenitipId.HasValue) throw new BusinessException("Pilih penitip tujuan retur.");
        if (req.Items.Count == 0) throw new BusinessException("Tambahkan minimal satu barang.");
        if (req.Items.Any(i => i.Jumlah <= 0)) throw new BusinessException("Jumlah setiap barang harus lebih dari 0.");
        if (req.Items.GroupBy(i => i.BarangId).Any(g => g.Count() > 1))
            throw new BusinessException("Ada barang yang dimasukkan dua kali.");

        var tanggal = DateTime.Now;
        var mutasi = req.Jenis.StartsWith("RETUR") ? "KELUAR_RETUR" : "KELUAR_RUSAK";

        return await _db.InTransactionAsync(async (conn, tx) =>
        {
            var no = await _trx.NextNumberAsync(conn, tx, "PG", tanggal);
            var id = await _trx.InsertPengeluaranAsync(conn, tx, no, tanggal, req.Jenis,
                req.Jenis == "RETUR_PEMASOK" ? req.PemasokId : null,
                req.Jenis == "RETUR_PENITIP" ? req.PenitipId : null, req.Alasan.Trim());

            foreach (var item in req.Items)
            {
                var b = await _barang.GetForUpdateAsync(conn, tx, item.BarangId)
                        ?? throw new BusinessException("Barang tidak ditemukan.");
                if (req.Jenis == "RETUR_PENITIP" && (!b.IsTitipan || b.PenitipId != req.PenitipId))
                    throw new BusinessException($"{b.Nama} bukan titipan dari penitip yang dipilih.");
                if (req.Jenis == "RETUR_PEMASOK" && b.IsTitipan)
                    throw new BusinessException($"{b.Nama} adalah barang titipan; gunakan Retur ke Penitip.");

                await _trx.InsertPengeluaranDetailAsync(conn, tx, id, b.BarangId, item.Jumlah);
                var saldo = await _barang.DecreaseAsync(conn, tx, b.BarangId, item.Jumlah)
                            ?? throw new BusinessException($"Stok {b.Nama} tidak cukup (tersedia {Fmt.Num(b.StokSaatIni)} {b.SatuanNama}).");
                await _barang.InsertMutasiAsync(conn, tx, b.BarangId, tanggal, mutasi, "PENGELUARAN", id, no,
                    0, item.Jumlah, saldo, Fmt.SumberLabel(req.Jenis) + ": " + req.Alasan.Trim());
            }
            await _aktivitas.InsertAsync(Session.UserId, "PENGELUARAN", "PengeluaranLain", no,
                $"{Fmt.SumberLabel(req.Jenis)}, {req.Items.Count} item", conn, tx);
            return no;
        });
    }
}
