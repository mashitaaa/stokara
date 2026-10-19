using System.Data;
using Stokara.Infrastructure;
using Stokara.Models;

namespace Stokara.Repositories;

/// <summary>Query agregat untuk dashboard (FR-04-07) dan laporan (FR-27). Semua angka dihitung dari database.</summary>
public sealed class ReportRepository
{
    private readonly Db _db;
    public ReportRepository(Db db) => _db = db;

    // ================================ Dashboard ================================

    public async Task<DashboardStats> StatsAsync(int? userId, string periode, int ambangHari)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleAsync(@"
SELECT
  (SELECT ISNULL(SUM(Total), 0) FROM dbo.Penjualan WHERE Status = 'AKTIF' AND Tanggal >= @Hari AND Tanggal < @Besok
       AND (@UserId IS NULL OR UserId = @UserId)) AS PenjualanHariIni,
  (SELECT COUNT(*) FROM dbo.Penjualan WHERE Status = 'AKTIF' AND Tanggal >= @Hari AND Tanggal < @Besok
       AND (@UserId IS NULL OR UserId = @UserId)) AS TransaksiHariIni,
  (SELECT ISNULL(SUM(StokSaatIni * HargaPokokRata), 0) FROM dbo.Barang WHERE IsActive = 1 AND Kepemilikan = 'MILIK') AS NilaiPersediaan,
  (SELECT COUNT(*) FROM dbo.Barang WHERE IsActive = 1 AND StokSaatIni <= StokMinimum) AS StokKritis,
  (SELECT COUNT(*) FROM dbo.Barang WHERE IsActive = 1) AS JumlahBarang,
  (SELECT ISNULL(SUM(NilaiHutang - JumlahDibayar), 0) FROM dbo.HutangPenitip WHERE Status IN ('BELUM','SEBAGIAN')) AS HutangPenitip,
  (SELECT ISNULL(SUM(JumlahTerpakai), 0) FROM dbo.PemakaianKuota WHERE Periode = @Periode) AS SubsidiTersalur,
  (SELECT ISNULL(SUM(a.JumlahKuota * ISNULL(c.Jml, 0)), 0)
     FROM dbo.AturanKuota a
     LEFT JOIN (SELECT JenisPenerima, COUNT(*) AS Jml FROM dbo.PenerimaSubsidi WHERE Status = 'AKTIF' GROUP BY JenisPenerima) c
            ON c.JenisPenerima = a.JenisPenerima
     WHERE a.Periode = @Periode) AS SubsidiKuota",
            r => new DashboardStats
            {
                PenjualanHariIni = r.Dec("PenjualanHariIni"),
                TransaksiHariIni = r.Int("TransaksiHariIni"),
                NilaiPersediaan = r.Dec("NilaiPersediaan"),
                StokKritis = r.Int("StokKritis"),
                JumlahBarang = r.Int("JumlahBarang"),
                HutangPenitip = r.Dec("HutangPenitip"),
                SubsidiTersalur = r.Dec("SubsidiTersalur"),
                SubsidiKuota = r.Dec("SubsidiKuota")
            },
            new { Hari = DateTime.Today, Besok = DateTime.Today.AddDays(1), UserId = userId, Periode = periode });
    }

    public async Task<List<ChartPoint>> PenjualanHarianAsync(int hari, int? userId)
    {
        var dari = DateTime.Today.AddDays(-(hari - 1));
        await using var conn = await _db.OpenAsync();
        var rows = await conn.QueryAsync(@"
SELECT CAST(Tanggal AS DATE) AS Hari, SUM(Total) AS Total FROM dbo.Penjualan
WHERE Status = 'AKTIF' AND Tanggal >= @Dari AND (@UserId IS NULL OR UserId = @UserId)
GROUP BY CAST(Tanggal AS DATE)",
            r => (Hari: r.Date("Hari"), Total: r.Dec("Total")), new { Dari = dari, UserId = userId });
        var map = rows.ToDictionary(x => x.Hari.Date, x => x.Total);
        var list = new List<ChartPoint>();
        for (var d = dari; d <= DateTime.Today; d = d.AddDays(1))
            list.Add(new ChartPoint(d.ToString(hari <= 7 ? "ddd" : "dd/MM", Fmt.Id), (double)(map.TryGetValue(d, out var v) ? v : 0m)));
        return list;
    }

    public async Task<List<ChartPoint>> TerlarisAsync(int hari, int top)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync($@"
SELECT TOP ({top}) b.Nama, SUM(d.Jumlah) AS Jumlah
FROM dbo.PenjualanDetail d JOIN dbo.Penjualan p ON p.PenjualanId = d.PenjualanId JOIN dbo.Barang b ON b.BarangId = d.BarangId
WHERE p.Status = 'AKTIF' AND p.Tanggal >= @Dari
GROUP BY b.Nama ORDER BY SUM(d.Jumlah) DESC",
            r => new ChartPoint(r.Str("Nama"), (double)r.Dec("Jumlah")), new { Dari = DateTime.Today.AddDays(-(hari - 1)) });
    }

    /// <summary>Kuota terpakai vs tersedia per barang subsidi (Value = terpakai, Value2 = total kuota).</summary>
    public async Task<List<ChartPoint>> KuotaPerBarangAsync(string periode)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync(@"
SELECT b.Nama,
       ISNULL((SELECT SUM(pk.JumlahTerpakai) FROM dbo.PemakaianKuota pk WHERE pk.BarangId = b.BarangId AND pk.Periode = @Periode), 0) AS Terpakai,
       SUM(a.JumlahKuota * ISNULL(c.Jml, 0)) AS Kuota
FROM dbo.AturanKuota a JOIN dbo.Barang b ON b.BarangId = a.BarangId
LEFT JOIN (SELECT JenisPenerima, COUNT(*) AS Jml FROM dbo.PenerimaSubsidi WHERE Status = 'AKTIF' GROUP BY JenisPenerima) c
       ON c.JenisPenerima = a.JenisPenerima
WHERE a.Periode = @Periode
GROUP BY b.BarangId, b.Nama ORDER BY b.Nama",
            r => new ChartPoint(r.Str("Nama"), (double)r.Dec("Terpakai"), (double)r.Dec("Kuota")), new { Periode = periode });
    }

    public async Task<List<PeringatanRow>> StokKritisAsync(int top)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync($@"
SELECT TOP ({top}) b.BarangId, b.Nama, s.Nama AS Satuan, b.StokSaatIni, b.StokMinimum
FROM dbo.Barang b JOIN dbo.Satuan s ON s.SatuanId = b.SatuanId
WHERE b.IsActive = 1 AND b.StokSaatIni <= b.StokMinimum
ORDER BY CASE WHEN b.StokSaatIni = 0 THEN 0 ELSE 1 END, b.StokSaatIni / NULLIF(b.StokMinimum, 0)",
            r => new PeringatanRow
            {
                BarangId = r.Int("BarangId"), Nama = r.Str("Nama"), Satuan = r.Str("Satuan"),
                Stok = r.Dec("StokSaatIni"), StokMinimum = r.Dec("StokMinimum"),
                Status = r.Dec("StokSaatIni") <= 0 ? "Habis" : "Menipis"
            });
    }

    /// <summary>
    /// Perkiraan kedaluwarsa (A-09): tanpa batch, sisa stok dianggap berasal dari penerimaan terbaru (FIFO keluar).
    /// Penerimaan yang "menutupi" sisa stok dicari dengan jumlah kumulatif dari yang terbaru.
    /// </summary>
    public async Task<List<PeringatanRow>> PerkiraanKedaluwarsaAsync(int ambangHari, int top = 50)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync($@"
WITH r AS (
    SELECT d.BarangId, d.Jumlah, d.TanggalKedaluwarsa,
           SUM(d.Jumlah) OVER (PARTITION BY d.BarangId ORDER BY p.Tanggal DESC, d.DetailId DESC ROWS UNBOUNDED PRECEDING) AS Kum
    FROM dbo.PenerimaanDetail d JOIN dbo.Penerimaan p ON p.PenerimaanId = d.PenerimaanId
    WHERE p.Status = 'AKTIF'
)
SELECT TOP ({top}) b.BarangId, b.Nama, s.Nama AS Satuan, b.StokSaatIni, b.StokMinimum, MIN(r.TanggalKedaluwarsa) AS Exp
FROM dbo.Barang b
JOIN dbo.Satuan s ON s.SatuanId = b.SatuanId
JOIN r ON r.BarangId = b.BarangId
WHERE b.IsActive = 1 AND b.StokSaatIni > 0 AND r.Kum - r.Jumlah < b.StokSaatIni AND r.TanggalKedaluwarsa IS NOT NULL
GROUP BY b.BarangId, b.Nama, s.Nama, b.StokSaatIni, b.StokMinimum
HAVING MIN(r.TanggalKedaluwarsa) <= DATEADD(DAY, @Ambang, CAST(GETDATE() AS DATE))
ORDER BY MIN(r.TanggalKedaluwarsa)",
            r => new PeringatanRow
            {
                BarangId = r.Int("BarangId"), Nama = r.Str("Nama"), Satuan = r.Str("Satuan"), Stok = r.Dec("StokSaatIni"),
                StokMinimum = r.Dec("StokMinimum"), Kedaluwarsa = r.DateN("Exp"),
                Status = r.Date("Exp") < DateTime.Today ? "Habis" : "Menipis"
            }, new { Ambang = ambangHari });
    }

    // ================================ Laporan ================================

    public async Task<DataTable> PenjualanPerBarangAsync(DateTime dari, DateTime sampai, string jenis, int? kategoriId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.TableAsync(@"
SELECT b.SKU, b.Nama AS [Barang], k.Nama AS [Kategori],
       CASE WHEN b.IsSubsidi = 1 THEN 'SUBSIDI' WHEN b.Kepemilikan = 'TITIPAN' THEN 'TITIPAN' ELSE 'BEBAS' END AS [Jenis],
       SUM(d.Jumlah) AS [Jumlah Terjual], s.Nama AS [Satuan],
       SUM(d.Subtotal) AS [Penjualan Rp],
       SUM(d.Jumlah * d.HargaPokok) AS [HPP atau Hak Penitip Rp],
       SUM(d.Subtotal - d.Jumlah * d.HargaPokok) AS [Laba Kotor Rp]
FROM dbo.PenjualanDetail d
JOIN dbo.Penjualan p ON p.PenjualanId = d.PenjualanId
JOIN dbo.Barang b ON b.BarangId = d.BarangId
JOIN dbo.Kategori k ON k.KategoriId = b.KategoriId
JOIN dbo.Satuan s ON s.SatuanId = b.SatuanId
WHERE p.Status = 'AKTIF' AND p.Tanggal >= @Dari AND p.Tanggal < @Sampai
  AND (@KategoriId IS NULL OR b.KategoriId = @KategoriId)
  AND (@Jenis IS NULL
       OR (@Jenis = 'SUBSIDI' AND p.Jenis = 'PENYALURAN_SUBSIDI')
       OR (@Jenis IN ('UMUM','ANGGOTA') AND p.Jenis = 'PENJUALAN' AND p.JenisPelanggan = @Jenis))
GROUP BY b.SKU, b.Nama, k.Nama, b.IsSubsidi, b.Kepemilikan, s.Nama
ORDER BY SUM(d.Subtotal) DESC",
            new { Dari = dari.Date, Sampai = sampai.Date.AddDays(1), Jenis = jenis, KategoriId = kategoriId });
    }

    public async Task<List<ChartPoint>> PenjualanPerHariAsync(DateTime dari, DateTime sampai)
    {
        await using var conn = await _db.OpenAsync();
        var rows = await conn.QueryAsync(@"
SELECT CAST(Tanggal AS DATE) AS Hari, SUM(Total) AS Total FROM dbo.Penjualan
WHERE Status = 'AKTIF' AND Tanggal >= @Dari AND Tanggal < @Sampai GROUP BY CAST(Tanggal AS DATE)",
            r => (Hari: r.Date("Hari"), Total: r.Dec("Total")), new { Dari = dari.Date, Sampai = sampai.Date.AddDays(1) });
        var map = rows.ToDictionary(x => x.Hari.Date, x => x.Total);
        var list = new List<ChartPoint>();
        if ((sampai.Date - dari.Date).TotalDays > 62)
        {
            foreach (var g in rows.GroupBy(x => new DateTime(x.Hari.Year, x.Hari.Month, 1)).OrderBy(g => g.Key))
                list.Add(new ChartPoint(g.Key.ToString("MMM yy", Fmt.Id), (double)g.Sum(x => x.Total)));
            return list;
        }
        for (var d = dari.Date; d <= sampai.Date; d = d.AddDays(1))
            list.Add(new ChartPoint(d.ToString("dd/MM", Fmt.Id), (double)(map.TryGetValue(d, out var v) ? v : 0m)));
        return list;
    }

    public async Task<DataTable> StokAsync(int? kategoriId, string jenis, string status, int ambangHari)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.TableAsync(@"
WITH r AS (
    SELECT d.BarangId, d.Jumlah, d.TanggalKedaluwarsa,
           SUM(d.Jumlah) OVER (PARTITION BY d.BarangId ORDER BY p.Tanggal DESC, d.DetailId DESC ROWS UNBOUNDED PRECEDING) AS Kum
    FROM dbo.PenerimaanDetail d JOIN dbo.Penerimaan p ON p.PenerimaanId = d.PenerimaanId WHERE p.Status = 'AKTIF'
), e AS (
    SELECT r.BarangId, MIN(r.TanggalKedaluwarsa) AS Exp
    FROM r JOIN dbo.Barang b ON b.BarangId = r.BarangId
    WHERE r.Kum - r.Jumlah < b.StokSaatIni AND r.TanggalKedaluwarsa IS NOT NULL
    GROUP BY r.BarangId
)
SELECT b.SKU, b.Nama AS [Barang], k.Nama AS [Kategori],
       CASE WHEN b.IsSubsidi = 1 THEN 'SUBSIDI' WHEN b.Kepemilikan = 'TITIPAN' THEN 'TITIPAN' ELSE 'BEBAS' END AS [Jenis],
       b.StokSaatIni AS [Stok], s.Nama AS [Satuan], b.StokMinimum AS [Stok Minimum],
       CASE WHEN b.StokSaatIni = 0 THEN 'Habis' WHEN b.StokSaatIni <= b.StokMinimum THEN 'Menipis' ELSE 'Aman' END AS [Status],
       CASE WHEN b.Kepemilikan = 'MILIK' THEN b.HargaPokokRata ELSE NULL END AS [HPP Rata-rata Rp],
       CASE WHEN b.Kepemilikan = 'MILIK' THEN b.StokSaatIni * b.HargaPokokRata ELSE 0 END AS [Nilai Persediaan Rp],
       e.Exp AS [Perkiraan Kedaluwarsa],
       CASE WHEN e.Exp IS NOT NULL AND e.Exp <= DATEADD(DAY, @Ambang, CAST(GETDATE() AS DATE)) THEN 'Ya' ELSE '' END AS [Peringatan]
FROM dbo.Barang b
JOIN dbo.Kategori k ON k.KategoriId = b.KategoriId
JOIN dbo.Satuan s ON s.SatuanId = b.SatuanId
LEFT JOIN e ON e.BarangId = b.BarangId
WHERE b.IsActive = 1
  AND (@KategoriId IS NULL OR b.KategoriId = @KategoriId)
  AND (@Jenis IS NULL OR (@Jenis = 'SUBSIDI' AND b.IsSubsidi = 1) OR (@Jenis = 'TITIPAN' AND b.Kepemilikan = 'TITIPAN')
       OR (@Jenis = 'BEBAS' AND b.IsSubsidi = 0 AND b.Kepemilikan = 'MILIK'))
  AND (@Status IS NULL OR (@Status = 'Habis' AND b.StokSaatIni = 0)
       OR (@Status = 'Menipis' AND b.StokSaatIni > 0 AND b.StokSaatIni <= b.StokMinimum)
       OR (@Status = 'Aman' AND b.StokSaatIni > b.StokMinimum))
ORDER BY k.Nama, b.Nama",
            new { KategoriId = kategoriId, Jenis = jenis, Status = status, Ambang = ambangHari });
    }

    public async Task<DataTable> SubsidiAsync(DateTime dari, DateTime sampai, int? barangId, string dusun)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.TableAsync(@"
SELECT ps.Nama AS [Penerima], CASE ps.JenisPenerima WHEN 'PETANI' THEN 'Petani' WHEN 'RUMAH_TANGGA' THEN 'Rumah Tangga'
           WHEN 'USAHA_MIKRO' THEN 'Usaha Mikro' ELSE 'Lainnya' END AS [Jenis Penerima],
       ISNULL(ps.Dusun, '-') AS [Dusun], b.Nama AS [Barang], d.PeriodeKuota AS [Periode Kuota],
       SUM(d.Jumlah) AS [Jumlah], s.Nama AS [Satuan], SUM(d.Subtotal) AS [Nilai Rp], COUNT(DISTINCT p.PenjualanId) AS [Transaksi]
FROM dbo.Penjualan p
JOIN dbo.PenjualanDetail d ON d.PenjualanId = p.PenjualanId
JOIN dbo.PenerimaSubsidi ps ON ps.PenerimaId = p.PenerimaId
JOIN dbo.Barang b ON b.BarangId = d.BarangId
JOIN dbo.Satuan s ON s.SatuanId = b.SatuanId
WHERE p.Status = 'AKTIF' AND p.Jenis = 'PENYALURAN_SUBSIDI' AND p.Tanggal >= @Dari AND p.Tanggal < @Sampai
  AND (@BarangId IS NULL OR d.BarangId = @BarangId) AND (@Dusun IS NULL OR ps.Dusun = @Dusun)
GROUP BY ps.Nama, ps.JenisPenerima, ps.Dusun, b.Nama, d.PeriodeKuota, s.Nama
ORDER BY ps.Dusun, ps.Nama, b.Nama",
            new { Dari = dari.Date, Sampai = sampai.Date.AddDays(1), BarangId = barangId, Dusun = dusun });
    }

    public async Task<List<string>> ListDusunAsync()
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync(
            "SELECT DISTINCT Dusun FROM dbo.PenerimaSubsidi WHERE Dusun IS NOT NULL AND Dusun <> '' ORDER BY Dusun", r => r.Str("Dusun"));
    }

    public async Task<DataTable> KonsinyasiAsync(DateTime dari, DateTime sampai, int? penitipId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.TableAsync(@"
SELECT pt.Nama AS [Penitip], b.Nama AS [Barang],
       b.StokSaatIni AS [Stok Titipan],
       ISNULL(SUM(CASE WHEN h.Status <> 'BATAL' AND h.CreatedAt >= @Dari AND h.CreatedAt < @Sampai THEN h.Jumlah END), 0) AS [Terjual],
       ISNULL(SUM(CASE WHEN h.Status <> 'BATAL' AND h.CreatedAt >= @Dari AND h.CreatedAt < @Sampai THEN h.NilaiHutang END), 0) AS [Hak Penitip Rp],
       ISNULL(SUM(CASE WHEN h.Status <> 'BATAL' AND h.CreatedAt >= @Dari AND h.CreatedAt < @Sampai THEN h.Komisi END), 0) AS [Komisi Koperasi Rp],
       ISNULL(SUM(CASE WHEN h.Status <> 'BATAL' THEN h.NilaiHutang - h.JumlahDibayar END), 0) AS [Sisa Hutang Semua Periode Rp]
FROM dbo.Barang b
JOIN dbo.Penitip pt ON pt.PenitipId = b.PenitipId
LEFT JOIN dbo.HutangPenitip h ON h.BarangId = b.BarangId
WHERE b.Kepemilikan = 'TITIPAN' AND (@PenitipId IS NULL OR b.PenitipId = @PenitipId)
GROUP BY pt.Nama, b.Nama, b.StokSaatIni
ORDER BY pt.Nama, b.Nama",
            new { Dari = dari.Date, Sampai = sampai.Date.AddDays(1), PenitipId = penitipId });
    }

    public async Task<DataTable> MutasiAsync(DateTime dari, DateTime sampai, int? barangId, string jenis)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.TableAsync(@"
SELECT m.Tanggal AS [Tanggal], b.SKU, b.Nama AS [Barang], m.JenisMutasi AS [Jenis Mutasi], m.NomorDokumen AS [Dokumen],
       m.Masuk AS [Masuk], m.Keluar AS [Keluar], m.SaldoSetelah AS [Saldo], ISNULL(m.Keterangan, '') AS [Keterangan],
       u.Nama AS [Pengguna]
FROM dbo.MutasiStok m JOIN dbo.Barang b ON b.BarangId = m.BarangId JOIN dbo.Users u ON u.UserId = m.UserId
WHERE m.Tanggal >= @Dari AND m.Tanggal < @Sampai
  AND (@BarangId IS NULL OR m.BarangId = @BarangId) AND (@Jenis IS NULL OR m.JenisMutasi = @Jenis)
ORDER BY m.Tanggal, m.MutasiId",
            new { Dari = dari.Date, Sampai = sampai.Date.AddDays(1), BarangId = barangId, Jenis = jenis });
    }

    /// <summary>Angka ringkasan untuk rekap bulanan.</summary>
    public async Task<Dictionary<string, decimal>> RekapAsync(DateTime dari, DateTime sampai, string periode)
    {
        await using var conn = await _db.OpenAsync();
        var row = await conn.QuerySingleAsync(@"
SELECT
 (SELECT COUNT(*) FROM dbo.Penjualan WHERE Status = 'AKTIF' AND Jenis = 'PENJUALAN' AND Tanggal >= @Dari AND Tanggal < @Sampai) AS TrxJual,
 (SELECT ISNULL(SUM(Total), 0) FROM dbo.Penjualan WHERE Status = 'AKTIF' AND Jenis = 'PENJUALAN' AND Tanggal >= @Dari AND Tanggal < @Sampai) AS NilaiJual,
 (SELECT COUNT(*) FROM dbo.Penjualan WHERE Status = 'AKTIF' AND Jenis = 'PENYALURAN_SUBSIDI' AND Tanggal >= @Dari AND Tanggal < @Sampai) AS TrxSubsidi,
 (SELECT ISNULL(SUM(Total), 0) FROM dbo.Penjualan WHERE Status = 'AKTIF' AND Jenis = 'PENYALURAN_SUBSIDI' AND Tanggal >= @Dari AND Tanggal < @Sampai) AS NilaiSubsidi,
 (SELECT COUNT(*) FROM dbo.Penjualan WHERE Status = 'DIBATALKAN' AND Tanggal >= @Dari AND Tanggal < @Sampai) AS TrxBatal,
 (SELECT ISNULL(SUM(d.Subtotal - d.Jumlah * d.HargaPokok), 0) FROM dbo.PenjualanDetail d JOIN dbo.Penjualan p ON p.PenjualanId = d.PenjualanId
     WHERE p.Status = 'AKTIF' AND p.Tanggal >= @Dari AND p.Tanggal < @Sampai) AS LabaKotor,
 (SELECT ISNULL(SUM(Komisi), 0) FROM dbo.HutangPenitip WHERE Status <> 'BATAL' AND CreatedAt >= @Dari AND CreatedAt < @Sampai) AS Komisi,
 (SELECT ISNULL(SUM(NilaiHutang), 0) FROM dbo.HutangPenitip WHERE Status <> 'BATAL' AND CreatedAt >= @Dari AND CreatedAt < @Sampai) AS HutangBaru,
 (SELECT ISNULL(SUM(JumlahBayar), 0) FROM dbo.PelunasanPenitip WHERE Tanggal >= @Dari AND Tanggal < @Sampai) AS Pelunasan,
 (SELECT ISNULL(SUM(NilaiHutang - JumlahDibayar), 0) FROM dbo.HutangPenitip WHERE Status IN ('BELUM','SEBAGIAN')) AS SisaHutang,
 (SELECT ISNULL(SUM(Total), 0) FROM dbo.Penerimaan WHERE Status = 'AKTIF' AND Sumber IN ('PEMBELIAN','SUBSIDI') AND Tanggal >= @Dari AND Tanggal < @Sampai) AS Pembelian,
 (SELECT COUNT(*) FROM dbo.Penerimaan WHERE Status = 'AKTIF' AND Tanggal >= @Dari AND Tanggal < @Sampai) AS TrxTerima,
 (SELECT COUNT(*) FROM dbo.PengeluaranLain WHERE Tanggal >= @Dari AND Tanggal < @Sampai) AS TrxKeluarLain,
 (SELECT ISNULL(SUM(StokSaatIni * HargaPokokRata), 0) FROM dbo.Barang WHERE IsActive = 1 AND Kepemilikan = 'MILIK') AS NilaiPersediaan,
 (SELECT ISNULL(SUM(JumlahTerpakai), 0) FROM dbo.PemakaianKuota WHERE Periode = @Periode) AS KuotaTerpakai,
 (SELECT COUNT(*) FROM dbo.StokOpname WHERE Status = 'DISETUJUI' AND Tanggal >= @Dari AND Tanggal < @Sampai) AS OpnameDisetujui,
 (SELECT ISNULL(SUM(ABS(m.Masuk - m.Keluar)), 0) FROM dbo.MutasiStok m WHERE m.JenisMutasi = 'PENYESUAIAN' AND m.Tanggal >= @Dari AND m.Tanggal < @Sampai) AS UnitPenyesuaian,
 (SELECT COUNT(*) FROM dbo.TutupHari WHERE Tanggal >= @Dari AND Tanggal < @Sampai) AS HariTutup,
 (SELECT ISNULL(SUM(Selisih), 0) FROM dbo.TutupHari WHERE Tanggal >= @Dari AND Tanggal < @Sampai) AS SelisihKas",
            r =>
            {
                var d = new Dictionary<string, decimal>();
                for (var i = 0; i < r.FieldCount; i++) d[r.GetName(i)] = Convert.ToDecimal(r.GetValue(i));
                return d;
            }, new { Dari = dari.Date, Sampai = sampai.Date.AddDays(1), Periode = periode });
        return row;
    }
}
