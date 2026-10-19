using Microsoft.Data.SqlClient;
using Stokara.Infrastructure;
using Stokara.Models;

namespace Stokara.Repositories;

/// <summary>Header/detail penjualan, penerimaan, pengeluaran, penomoran dokumen, dan tutup hari.</summary>
public sealed class TransaksiRepository
{
    private readonly Db _db;
    public TransaksiRepository(Db db) => _db = db;

    /// <summary>Nomor dokumen otomatis dan unik per prefix per hari (FR-19), mis. PJ-20261001-0001.</summary>
    public async Task<string> NextNumberAsync(SqlConnection conn, SqlTransaction tx, string prefix, DateTime tanggal)
    {
        var urut = await conn.ScalarAsync<int>(@"
MERGE dbo.NomorUrut WITH (HOLDLOCK) AS t
USING (SELECT @Prefix AS Prefix, @Tanggal AS Tanggal) AS s
   ON t.Prefix = s.Prefix AND t.Tanggal = s.Tanggal
WHEN MATCHED THEN UPDATE SET Terakhir = t.Terakhir + 1
WHEN NOT MATCHED THEN INSERT (Prefix, Tanggal, Terakhir) VALUES (s.Prefix, s.Tanggal, 1)
OUTPUT inserted.Terakhir;", new { Prefix = prefix, Tanggal = tanggal.Date }, tx);
        return $"{prefix}-{tanggal:yyyyMMdd}-{urut:0000}";
    }

    public async Task<bool> IsDayClosedAsync(DateTime tanggal, SqlConnection conn = null, SqlTransaction tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM dbo.TutupHari WHERE Tanggal = @T";
        if (conn != null) return await conn.ScalarAsync<int>(sql, new { T = tanggal.Date }, tx) > 0;
        await using var c = await _db.OpenAsync();
        return await c.ScalarAsync<int>(sql, new { T = tanggal.Date }) > 0;
    }

    // ============================== Penjualan ==============================

    public Task<int> InsertPenjualanAsync(SqlConnection conn, SqlTransaction tx, string nomor, DateTime tanggal, string jenis,
        string jenisPelanggan, int? anggotaId, int? penerimaId, decimal total, decimal uang, decimal kembalian)
        => conn.ScalarAsync<int>(@"
INSERT INTO dbo.Penjualan (NomorDokumen, Tanggal, Jenis, JenisPelanggan, AnggotaId, PenerimaId, Total, UangDiterima, Kembalian, UserId)
OUTPUT inserted.PenjualanId
VALUES (@Nomor, @Tanggal, @Jenis, @JenisPelanggan, @AnggotaId, @PenerimaId, @Total, @Uang, @Kembalian, @UserId)",
            new
            {
                Nomor = nomor, Tanggal = tanggal, Jenis = jenis, JenisPelanggan = jenisPelanggan, AnggotaId = anggotaId,
                PenerimaId = penerimaId, Total = total, Uang = uang, Kembalian = kembalian, UserId = Session.UserId
            }, tx);

    public Task<int> InsertPenjualanDetailAsync(SqlConnection conn, SqlTransaction tx, int penjualanId, int barangId,
        decimal jumlah, decimal harga, decimal hargaPokok, string periodeKuota)
        => conn.ScalarAsync<int>(@"
INSERT INTO dbo.PenjualanDetail (PenjualanId, BarangId, Jumlah, HargaSatuan, HargaPokok, Subtotal, PeriodeKuota)
OUTPUT inserted.DetailId
VALUES (@PenjualanId, @BarangId, @Jumlah, @Harga, @HargaPokok, @Subtotal, @Periode)",
            new
            {
                PenjualanId = penjualanId, BarangId = barangId, Jumlah = jumlah, Harga = harga, HargaPokok = hargaPokok,
                Subtotal = jumlah * harga, Periode = periodeKuota
            }, tx);

    public sealed class PenjualanHeader
    {
        public int PenjualanId { get; set; }
        public string NomorDokumen { get; set; }
        public DateTime Tanggal { get; set; }
        public string Jenis { get; set; }
        public int? PenerimaId { get; set; }
        public string Status { get; set; }
        public decimal Total { get; set; }
        public decimal UangDiterima { get; set; }
        public decimal Kembalian { get; set; }
        public string Pihak { get; set; }
        public string Kasir { get; set; }
    }

    public sealed class PenjualanLine
    {
        public int DetailId { get; set; }
        public int BarangId { get; set; }
        public decimal Jumlah { get; set; }
        public string PeriodeKuota { get; set; }
        public int? HutangId { get; set; }
        public decimal HutangDibayar { get; set; }
    }

    private const string HeaderSql = @"
SELECT p.PenjualanId, p.NomorDokumen, p.Tanggal, p.Jenis, p.PenerimaId, p.Status, p.Total, p.UangDiterima, p.Kembalian,
       COALESCE(ps.Nama, a.Nama + ' (Anggota)', 'Umum') AS Pihak, u.Nama AS Kasir
FROM dbo.Penjualan p
JOIN dbo.Users u ON u.UserId = p.UserId
LEFT JOIN dbo.Anggota a ON a.AnggotaId = p.AnggotaId
LEFT JOIN dbo.PenerimaSubsidi ps ON ps.PenerimaId = p.PenerimaId";

    private static PenjualanHeader MapHeader(SqlDataReader r) => new()
    {
        PenjualanId = r.Int("PenjualanId"),
        NomorDokumen = r.Str("NomorDokumen"),
        Tanggal = r.Date("Tanggal"),
        Jenis = r.Str("Jenis"),
        PenerimaId = r.IntN("PenerimaId"),
        Status = r.Str("Status"),
        Total = r.Dec("Total"),
        UangDiterima = r.Dec("UangDiterima"),
        Kembalian = r.Dec("Kembalian"),
        Pihak = r.Str("Pihak"),
        Kasir = r.Str("Kasir")
    };

    public Task<PenjualanHeader> GetPenjualanForUpdateAsync(SqlConnection conn, SqlTransaction tx, int id)
        => conn.QuerySingleAsync(HeaderSql.Replace("FROM dbo.Penjualan p", "FROM dbo.Penjualan p WITH (UPDLOCK, ROWLOCK)")
                                 + " WHERE p.PenjualanId = @Id", MapHeader, new { Id = id }, tx);

    public async Task<PenjualanHeader> GetPenjualanAsync(int id)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleAsync(HeaderSql + " WHERE p.PenjualanId = @Id", MapHeader, new { Id = id });
    }

    public Task<List<PenjualanLine>> GetPenjualanLinesAsync(SqlConnection conn, SqlTransaction tx, int penjualanId)
        => conn.QueryAsync(@"
SELECT d.DetailId, d.BarangId, d.Jumlah, d.PeriodeKuota, h.HutangId, ISNULL(h.JumlahDibayar, 0) AS HutangDibayar
FROM dbo.PenjualanDetail d
LEFT JOIN dbo.HutangPenitip h WITH (UPDLOCK) ON h.PenjualanDetailId = d.DetailId
WHERE d.PenjualanId = @Id",
            r => new PenjualanLine
            {
                DetailId = r.Int("DetailId"),
                BarangId = r.Int("BarangId"),
                Jumlah = r.Dec("Jumlah"),
                PeriodeKuota = r.Str("PeriodeKuota"),
                HutangId = r.IntN("HutangId"),
                HutangDibayar = r.Dec("HutangDibayar")
            }, new { Id = penjualanId }, tx);

    public Task MarkPenjualanVoidAsync(SqlConnection conn, SqlTransaction tx, int id, string alasan)
        => conn.ExecAsync(@"
UPDATE dbo.Penjualan SET Status = 'DIBATALKAN', AlasanBatal = @Alasan, DibatalkanOleh = @UserId, DibatalkanPada = SYSDATETIME()
WHERE PenjualanId = @Id", new { Id = id, Alasan = alasan, UserId = Session.UserId }, tx);

    public async Task<List<TransaksiRow>> ListPenjualanAsync(DateTime dari, DateTime sampai, string jenis, string keyword, int? userId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync(@"
SELECT p.PenjualanId AS Id, p.NomorDokumen, p.Tanggal, p.Jenis, p.Total, p.Status, p.UserId, p.AlasanBatal,
       u.Nama AS Pengguna, COALESCE(ps.Nama, a.Nama + ' (Anggota)', 'Umum') AS Pihak
FROM dbo.Penjualan p
JOIN dbo.Users u ON u.UserId = p.UserId
LEFT JOIN dbo.Anggota a ON a.AnggotaId = p.AnggotaId
LEFT JOIN dbo.PenerimaSubsidi ps ON ps.PenerimaId = p.PenerimaId
WHERE p.Tanggal >= @Dari AND p.Tanggal < @Sampai
  AND (@Jenis IS NULL OR p.Jenis = @Jenis)
  AND (@UserId IS NULL OR p.UserId = @UserId)
  AND (@Kw IS NULL OR p.NomorDokumen LIKE @Kw OR a.Nama LIKE @Kw OR ps.Nama LIKE @Kw)
ORDER BY p.Tanggal DESC",
            MapRow, new
            {
                Dari = dari.Date, Sampai = sampai.Date.AddDays(1), Jenis = jenis, UserId = userId,
                Kw = string.IsNullOrWhiteSpace(keyword) ? null : "%" + keyword.Trim() + "%"
            });
    }

    public async Task<List<TransaksiDetailRow>> PenjualanDetailAsync(int penjualanId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync(@"
SELECT b.Nama AS BarangNama, s.Nama AS Satuan, d.Jumlah, d.HargaSatuan AS Harga, d.Subtotal,
       CASE WHEN b.IsSubsidi = 1 THEN 'SUBSIDI' WHEN b.Kepemilikan = 'TITIPAN' THEN 'TITIPAN' ELSE '' END AS Keterangan
FROM dbo.PenjualanDetail d JOIN dbo.Barang b ON b.BarangId = d.BarangId JOIN dbo.Satuan s ON s.SatuanId = b.SatuanId
WHERE d.PenjualanId = @Id ORDER BY d.DetailId", MapDetail, new { Id = penjualanId });
    }

    // ============================== Penerimaan ==============================

    public Task<int> InsertPenerimaanAsync(SqlConnection conn, SqlTransaction tx, string nomor, DateTime tanggal, string sumber,
        int? pemasokId, int? penitipId, string suratJalan, string catatan, decimal total)
        => conn.ScalarAsync<int>(@"
INSERT INTO dbo.Penerimaan (NomorDokumen, Tanggal, Sumber, PemasokId, PenitipId, NomorSuratJalan, Catatan, Total, UserId)
OUTPUT inserted.PenerimaanId
VALUES (@Nomor, @Tanggal, @Sumber, @PemasokId, @PenitipId, @SuratJalan, @Catatan, @Total, @UserId)",
            new
            {
                Nomor = nomor, Tanggal = tanggal, Sumber = sumber, PemasokId = pemasokId, PenitipId = penitipId,
                SuratJalan = suratJalan, Catatan = catatan, Total = total, UserId = Session.UserId
            }, tx);

    public Task InsertPenerimaanDetailAsync(SqlConnection conn, SqlTransaction tx, int penerimaanId, int barangId,
        decimal jumlah, decimal harga, DateTime? kedaluwarsa)
        => conn.ExecAsync(@"
INSERT INTO dbo.PenerimaanDetail (PenerimaanId, BarangId, Jumlah, HargaBeli, TanggalKedaluwarsa)
VALUES (@PenerimaanId, @BarangId, @Jumlah, @Harga, @Exp)",
            new { PenerimaanId = penerimaanId, BarangId = barangId, Jumlah = jumlah, Harga = harga, Exp = kedaluwarsa?.Date }, tx);

    public sealed class PenerimaanHeader
    {
        public int PenerimaanId { get; set; }
        public string NomorDokumen { get; set; }
        public DateTime Tanggal { get; set; }
        public string Sumber { get; set; }
        public string Status { get; set; }
    }

    public sealed class PenerimaanLine
    {
        public int BarangId { get; set; }
        public decimal Jumlah { get; set; }
        public decimal HargaBeli { get; set; }
        public string Kepemilikan { get; set; }
    }

    public Task<PenerimaanHeader> GetPenerimaanForUpdateAsync(SqlConnection conn, SqlTransaction tx, int id)
        => conn.QuerySingleAsync(@"
SELECT PenerimaanId, NomorDokumen, Tanggal, Sumber, Status FROM dbo.Penerimaan WITH (UPDLOCK, ROWLOCK) WHERE PenerimaanId = @Id",
            r => new PenerimaanHeader
            {
                PenerimaanId = r.Int("PenerimaanId"), NomorDokumen = r.Str("NomorDokumen"), Tanggal = r.Date("Tanggal"),
                Sumber = r.Str("Sumber"), Status = r.Str("Status")
            }, new { Id = id }, tx);

    public Task<List<PenerimaanLine>> GetPenerimaanLinesAsync(SqlConnection conn, SqlTransaction tx, int id)
        => conn.QueryAsync(@"
SELECT d.BarangId, d.Jumlah, d.HargaBeli, b.Kepemilikan
FROM dbo.PenerimaanDetail d JOIN dbo.Barang b ON b.BarangId = d.BarangId WHERE d.PenerimaanId = @Id",
            r => new PenerimaanLine
            {
                BarangId = r.Int("BarangId"), Jumlah = r.Dec("Jumlah"), HargaBeli = r.Dec("HargaBeli"),
                Kepemilikan = r.Str("Kepemilikan")
            }, new { Id = id }, tx);

    public Task MarkPenerimaanVoidAsync(SqlConnection conn, SqlTransaction tx, int id, string alasan)
        => conn.ExecAsync(@"
UPDATE dbo.Penerimaan SET Status = 'DIBATALKAN', AlasanBatal = @Alasan, DibatalkanOleh = @UserId, DibatalkanPada = SYSDATETIME()
WHERE PenerimaanId = @Id", new { Id = id, Alasan = alasan, UserId = Session.UserId }, tx);

    public async Task<List<TransaksiRow>> ListPenerimaanAsync(DateTime dari, DateTime sampai, string sumber, string keyword)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync(@"
SELECT p.PenerimaanId AS Id, p.NomorDokumen, p.Tanggal, p.Sumber AS Jenis, p.Total, p.Status, p.UserId, p.AlasanBatal,
       u.Nama AS Pengguna,
       COALESCE(pm.Nama, pt.Nama, '-') + CASE WHEN p.NomorSuratJalan IS NULL THEN '' ELSE ' · SJ ' + p.NomorSuratJalan END AS Pihak
FROM dbo.Penerimaan p
JOIN dbo.Users u ON u.UserId = p.UserId
LEFT JOIN dbo.Pemasok pm ON pm.PemasokId = p.PemasokId
LEFT JOIN dbo.Penitip pt ON pt.PenitipId = p.PenitipId
WHERE p.Tanggal >= @Dari AND p.Tanggal < @Sampai
  AND (@Sumber IS NULL OR p.Sumber = @Sumber)
  AND (@Kw IS NULL OR p.NomorDokumen LIKE @Kw OR p.NomorSuratJalan LIKE @Kw OR pm.Nama LIKE @Kw OR pt.Nama LIKE @Kw)
ORDER BY p.Tanggal DESC",
            MapRow, new
            {
                Dari = dari.Date, Sampai = sampai.Date.AddDays(1), Sumber = sumber,
                Kw = string.IsNullOrWhiteSpace(keyword) ? null : "%" + keyword.Trim() + "%"
            });
    }

    public async Task<List<TransaksiDetailRow>> PenerimaanDetailAsync(int id)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync(@"
SELECT b.Nama AS BarangNama, s.Nama AS Satuan, d.Jumlah, d.HargaBeli AS Harga, d.Jumlah * d.HargaBeli AS Subtotal,
       CASE WHEN d.TanggalKedaluwarsa IS NULL THEN '' ELSE 'Exp ' + CONVERT(VARCHAR(10), d.TanggalKedaluwarsa, 103) END AS Keterangan
FROM dbo.PenerimaanDetail d JOIN dbo.Barang b ON b.BarangId = d.BarangId JOIN dbo.Satuan s ON s.SatuanId = b.SatuanId
WHERE d.PenerimaanId = @Id ORDER BY d.DetailId", MapDetail, new { Id = id });
    }

    // ============================== Pengeluaran lain ==============================

    public Task<int> InsertPengeluaranAsync(SqlConnection conn, SqlTransaction tx, string nomor, DateTime tanggal, string jenis,
        int? pemasokId, int? penitipId, string alasan)
        => conn.ScalarAsync<int>(@"
INSERT INTO dbo.PengeluaranLain (NomorDokumen, Tanggal, Jenis, PemasokId, PenitipId, Alasan, UserId)
OUTPUT inserted.PengeluaranId
VALUES (@Nomor, @Tanggal, @Jenis, @PemasokId, @PenitipId, @Alasan, @UserId)",
            new
            {
                Nomor = nomor, Tanggal = tanggal, Jenis = jenis, PemasokId = pemasokId, PenitipId = penitipId,
                Alasan = alasan, UserId = Session.UserId
            }, tx);

    public Task InsertPengeluaranDetailAsync(SqlConnection conn, SqlTransaction tx, int id, int barangId, decimal jumlah)
        => conn.ExecAsync(@"INSERT INTO dbo.PengeluaranDetail (PengeluaranId, BarangId, Jumlah) VALUES (@Id, @BarangId, @Jumlah)",
            new { Id = id, BarangId = barangId, Jumlah = jumlah }, tx);

    public async Task<List<TransaksiRow>> ListPengeluaranAsync(DateTime dari, DateTime sampai, string jenis)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync(@"
SELECT g.PengeluaranId AS Id, g.NomorDokumen, g.Tanggal, g.Jenis, 0 AS Total, 'AKTIF' AS Status, g.UserId,
       g.Alasan AS AlasanBatal, u.Nama AS Pengguna, COALESCE(pm.Nama, pt.Nama, '-') + ' · ' + g.Alasan AS Pihak
FROM dbo.PengeluaranLain g
JOIN dbo.Users u ON u.UserId = g.UserId
LEFT JOIN dbo.Pemasok pm ON pm.PemasokId = g.PemasokId
LEFT JOIN dbo.Penitip pt ON pt.PenitipId = g.PenitipId
WHERE g.Tanggal >= @Dari AND g.Tanggal < @Sampai AND (@Jenis IS NULL OR g.Jenis = @Jenis)
ORDER BY g.Tanggal DESC",
            MapRow, new { Dari = dari.Date, Sampai = sampai.Date.AddDays(1), Jenis = jenis });
    }

    public async Task<List<TransaksiDetailRow>> PengeluaranDetailAsync(int id)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync(@"
SELECT b.Nama AS BarangNama, s.Nama AS Satuan, d.Jumlah, 0 AS Harga, 0 AS Subtotal, '' AS Keterangan
FROM dbo.PengeluaranDetail d JOIN dbo.Barang b ON b.BarangId = d.BarangId JOIN dbo.Satuan s ON s.SatuanId = b.SatuanId
WHERE d.PengeluaranId = @Id", MapDetail, new { Id = id });
    }

    // ============================== Tutup hari ==============================

    public async Task<(int Jumlah, decimal Total)> RingkasanHariAsync(DateTime tanggal)
    {
        await using var conn = await _db.OpenAsync();
        var row = await conn.QuerySingleAsync(@"
SELECT COUNT(*) AS Jumlah, ISNULL(SUM(Total), 0) AS Total FROM dbo.Penjualan
WHERE Status = 'AKTIF' AND Tanggal >= @Dari AND Tanggal < @Sampai",
            r => (r.Int("Jumlah"), r.Dec("Total")), new { Dari = tanggal.Date, Sampai = tanggal.Date.AddDays(1) });
        return row;
    }

    public Task InsertTutupHariAsync(SqlConnection conn, SqlTransaction tx, DateTime tanggal, int jumlah, decimal total,
        decimal modal, decimal seharusnya, decimal fisik, string catatan)
        => conn.ExecAsync(@"
INSERT INTO dbo.TutupHari (Tanggal, JumlahTransaksi, TotalPenjualan, ModalAwal, UangSeharusnya, UangFisik, Catatan, UserId)
VALUES (@Tanggal, @Jumlah, @Total, @Modal, @Seharusnya, @Fisik, @Catatan, @UserId)",
            new
            {
                Tanggal = tanggal.Date, Jumlah = jumlah, Total = total, Modal = modal, Seharusnya = seharusnya,
                Fisik = fisik, Catatan = catatan, UserId = Session.UserId
            }, tx);

    public async Task<List<TutupHariRow>> ListTutupHariAsync(int top = 60)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync($@"
SELECT TOP ({top}) t.Tanggal, t.JumlahTransaksi, t.TotalPenjualan, t.ModalAwal, t.UangSeharusnya, t.UangFisik, t.Selisih,
       t.Catatan, u.Nama AS Pengguna
FROM dbo.TutupHari t JOIN dbo.Users u ON u.UserId = t.UserId ORDER BY t.Tanggal DESC",
            r => new TutupHariRow
            {
                Tanggal = r.Date("Tanggal"), JumlahTransaksi = r.Int("JumlahTransaksi"), TotalPenjualan = r.Dec("TotalPenjualan"),
                ModalAwal = r.Dec("ModalAwal"), UangSeharusnya = r.Dec("UangSeharusnya"), UangFisik = r.Dec("UangFisik"),
                Selisih = r.Dec("Selisih"), Catatan = r.Str("Catatan"), Pengguna = r.Str("Pengguna")
            });
    }

    private static TransaksiRow MapRow(SqlDataReader r) => new()
    {
        Id = r.Int("Id"),
        NomorDokumen = r.Str("NomorDokumen"),
        Tanggal = r.Date("Tanggal"),
        Jenis = r.Str("Jenis"),
        Total = r.Dec("Total"),
        Status = r.Str("Status"),
        UserId = r.Int("UserId"),
        AlasanBatal = r.Str("AlasanBatal"),
        Pengguna = r.Str("Pengguna"),
        Pihak = r.Str("Pihak")
    };

    private static TransaksiDetailRow MapDetail(SqlDataReader r) => new()
    {
        BarangNama = r.Str("BarangNama"),
        Satuan = r.Str("Satuan"),
        Jumlah = r.Dec("Jumlah"),
        Harga = r.Dec("Harga"),
        Subtotal = r.Dec("Subtotal"),
        Keterangan = r.Str("Keterangan")
    };
}
