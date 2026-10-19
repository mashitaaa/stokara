using Microsoft.Data.SqlClient;
using Stokara.Infrastructure;
using Stokara.Models;

namespace Stokara.Repositories;

/// <summary>Hutang ke penitip dan pelunasan FIFO (A-05, A-07).</summary>
public sealed class KonsinyasiRepository
{
    private readonly Db _db;
    public KonsinyasiRepository(Db db) => _db = db;

    public Task InsertHutangAsync(SqlConnection conn, SqlTransaction tx, int penitipId, int detailId, int barangId,
        decimal jumlah, decimal hargaTitip, decimal hargaJual)
        => conn.ExecAsync(@"
INSERT INTO dbo.HutangPenitip (PenitipId, PenjualanDetailId, BarangId, Jumlah, HargaTitip, NilaiHutang, Komisi)
VALUES (@PenitipId, @DetailId, @BarangId, @Jumlah, @HargaTitip, @Nilai, @Komisi)",
            new
            {
                PenitipId = penitipId, DetailId = detailId, BarangId = barangId, Jumlah = jumlah, HargaTitip = hargaTitip,
                Nilai = jumlah * hargaTitip, Komisi = (hargaJual - hargaTitip) * jumlah
            }, tx);

    public Task CancelHutangAsync(SqlConnection conn, SqlTransaction tx, int hutangId)
        => conn.ExecAsync("UPDATE dbo.HutangPenitip SET Status = 'BATAL' WHERE HutangId = @Id AND JumlahDibayar = 0",
            new { Id = hutangId }, tx);

    public async Task<List<PenitipRingkas>> RingkasanAsync()
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync(@"
SELECT p.PenitipId, p.Nama,
       ISNULL(SUM(CASE WHEN h.Status <> 'BATAL' THEN h.NilaiHutang END), 0) AS TotalHutang,
       ISNULL(SUM(CASE WHEN h.Status <> 'BATAL' THEN h.JumlahDibayar END), 0) AS TotalDibayar,
       ISNULL(SUM(CASE WHEN h.Status <> 'BATAL' THEN h.Komisi END), 0) AS TotalKomisi,
       ISNULL((SELECT SUM(b.StokSaatIni * b.HargaTitip) FROM dbo.Barang b
               WHERE b.PenitipId = p.PenitipId AND b.Kepemilikan = 'TITIPAN' AND b.IsActive = 1), 0) AS StokTitipanNilai
FROM dbo.Penitip p
LEFT JOIN dbo.HutangPenitip h ON h.PenitipId = p.PenitipId
WHERE p.IsActive = 1
GROUP BY p.PenitipId, p.Nama
ORDER BY p.Nama",
            r => new PenitipRingkas
            {
                PenitipId = r.Int("PenitipId"), Nama = r.Str("Nama"), TotalHutang = r.Dec("TotalHutang"),
                TotalDibayar = r.Dec("TotalDibayar"), TotalKomisi = r.Dec("TotalKomisi"),
                StokTitipanNilai = r.Dec("StokTitipanNilai")
            });
    }

    public async Task<List<HutangRow>> ListHutangAsync(int penitipId, bool hanyaBelumLunas)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync(@"
SELECT h.HutangId, h.CreatedAt AS Tanggal, pj.NomorDokumen, b.Nama AS BarangNama, h.Jumlah, h.HargaTitip, h.NilaiHutang,
       h.Komisi, h.JumlahDibayar, h.Status
FROM dbo.HutangPenitip h
JOIN dbo.PenjualanDetail d ON d.DetailId = h.PenjualanDetailId
JOIN dbo.Penjualan pj ON pj.PenjualanId = d.PenjualanId
JOIN dbo.Barang b ON b.BarangId = h.BarangId
WHERE h.PenitipId = @PenitipId AND (@Belum = 0 OR h.Status IN ('BELUM','SEBAGIAN'))
ORDER BY h.CreatedAt, h.HutangId",
            r => new HutangRow
            {
                HutangId = r.Int("HutangId"), Tanggal = r.Date("Tanggal"), NomorDokumen = r.Str("NomorDokumen"),
                BarangNama = r.Str("BarangNama"), Jumlah = r.Dec("Jumlah"), HargaTitip = r.Dec("HargaTitip"),
                NilaiHutang = r.Dec("NilaiHutang"), Komisi = r.Dec("Komisi"), JumlahDibayar = r.Dec("JumlahDibayar"),
                Status = r.Str("Status")
            }, new { PenitipId = penitipId, Belum = hanyaBelumLunas });
    }

    public sealed class OpenDebt
    {
        public int HutangId { get; set; }
        public decimal NilaiHutang { get; set; }
        public decimal JumlahDibayar { get; set; }
    }

    /// <summary>Hutang yang belum lunas, terurut dari yang tertua (FIFO), dikunci untuk diperbarui.</summary>
    public Task<List<OpenDebt>> OpenDebtsForUpdateAsync(SqlConnection conn, SqlTransaction tx, int penitipId)
        => conn.QueryAsync(@"
SELECT HutangId, NilaiHutang, JumlahDibayar FROM dbo.HutangPenitip WITH (UPDLOCK, ROWLOCK)
WHERE PenitipId = @PenitipId AND Status IN ('BELUM','SEBAGIAN')
ORDER BY CreatedAt, HutangId",
            r => new OpenDebt { HutangId = r.Int("HutangId"), NilaiHutang = r.Dec("NilaiHutang"), JumlahDibayar = r.Dec("JumlahDibayar") },
            new { PenitipId = penitipId }, tx);

    public Task<int> InsertPelunasanAsync(SqlConnection conn, SqlTransaction tx, string nomor, int penitipId, decimal jumlah, string catatan)
        => conn.ScalarAsync<int>(@"
INSERT INTO dbo.PelunasanPenitip (NomorDokumen, PenitipId, Tanggal, JumlahBayar, Catatan, UserId)
OUTPUT inserted.PelunasanId VALUES (@Nomor, @PenitipId, SYSDATETIME(), @Jumlah, @Catatan, @UserId)",
            new { Nomor = nomor, PenitipId = penitipId, Jumlah = jumlah, Catatan = catatan, UserId = Session.UserId }, tx);

    public Task AllocateAsync(SqlConnection conn, SqlTransaction tx, int pelunasanId, int hutangId, decimal jumlah)
        => conn.ExecAsync(@"
INSERT INTO dbo.PelunasanAlokasi (PelunasanId, HutangId, Jumlah) VALUES (@PelunasanId, @HutangId, @Jumlah);
UPDATE dbo.HutangPenitip SET
    JumlahDibayar = JumlahDibayar + @Jumlah,
    Status = CASE WHEN JumlahDibayar + @Jumlah >= NilaiHutang THEN 'LUNAS' ELSE 'SEBAGIAN' END
WHERE HutangId = @HutangId;", new { PelunasanId = pelunasanId, HutangId = hutangId, Jumlah = jumlah }, tx);

    public async Task<List<PelunasanRow>> ListPelunasanAsync(int? penitipId, int top = 200)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync($@"
SELECT TOP ({top}) pl.NomorDokumen, pl.Tanggal, p.Nama AS PenitipNama, pl.JumlahBayar, pl.Catatan, u.Nama AS Pengguna
FROM dbo.PelunasanPenitip pl JOIN dbo.Penitip p ON p.PenitipId = pl.PenitipId JOIN dbo.Users u ON u.UserId = pl.UserId
WHERE (@PenitipId IS NULL OR pl.PenitipId = @PenitipId)
ORDER BY pl.Tanggal DESC",
            r => new PelunasanRow
            {
                NomorDokumen = r.Str("NomorDokumen"), Tanggal = r.Date("Tanggal"), PenitipNama = r.Str("PenitipNama"),
                JumlahBayar = r.Dec("JumlahBayar"), Catatan = r.Str("Catatan"), Pengguna = r.Str("Pengguna")
            }, new { PenitipId = penitipId });
    }
}

/// <summary>Sesi stok opname dan detailnya.</summary>
public sealed class OpnameRepository
{
    private readonly Db _db;
    public OpnameRepository(Db db) => _db = db;

    private const string HeaderSql = @"
SELECT o.OpnameId, o.NomorDokumen, o.Tanggal, ISNULL(k.Nama, 'Semua kategori') AS Kategori, o.Status, o.Catatan, o.AlasanTolak,
       ub.Nama AS DibuatOleh, o.DiajukanOleh AS DiajukanOlehId, ua.Nama AS DiajukanOleh, us.Nama AS DisetujuiOleh,
       (SELECT COUNT(*) FROM dbo.StokOpnameDetail d WHERE d.OpnameId = o.OpnameId) AS JumlahBarang,
       (SELECT COUNT(*) FROM dbo.StokOpnameDetail d WHERE d.OpnameId = o.OpnameId AND d.Selisih <> 0) AS JumlahSelisih
FROM dbo.StokOpname o
LEFT JOIN dbo.Kategori k ON k.KategoriId = o.KategoriId
JOIN dbo.Users ub ON ub.UserId = o.DibuatOleh
LEFT JOIN dbo.Users ua ON ua.UserId = o.DiajukanOleh
LEFT JOIN dbo.Users us ON us.UserId = o.DisetujuiOleh";

    private static OpnameRow MapHeader(SqlDataReader r) => new()
    {
        OpnameId = r.Int("OpnameId"),
        NomorDokumen = r.Str("NomorDokumen"),
        Tanggal = r.Date("Tanggal"),
        Kategori = r.Str("Kategori"),
        Status = r.Str("Status"),
        Catatan = r.Str("Catatan"),
        AlasanTolak = r.Str("AlasanTolak"),
        DibuatOleh = r.Str("DibuatOleh"),
        DiajukanOlehId = r.IntN("DiajukanOlehId"),
        DiajukanOleh = r.Str("DiajukanOleh"),
        DisetujuiOleh = r.Str("DisetujuiOleh"),
        JumlahBarang = r.Int("JumlahBarang"),
        JumlahSelisih = r.Int("JumlahSelisih")
    };

    public async Task<List<OpnameRow>> ListAsync(int top = 100)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync(HeaderSql.Replace("SELECT o.OpnameId", $"SELECT TOP ({top}) o.OpnameId")
                                     + " ORDER BY o.Tanggal DESC", MapHeader);
    }

    public async Task<OpnameRow> GetAsync(int id, SqlConnection conn = null, SqlTransaction tx = null)
    {
        if (conn != null) return await conn.QuerySingleAsync(HeaderSql + " WHERE o.OpnameId = @Id", MapHeader, new { Id = id }, tx);
        await using var c = await _db.OpenAsync();
        return await c.QuerySingleAsync(HeaderSql + " WHERE o.OpnameId = @Id", MapHeader, new { Id = id });
    }

    public Task<string> GetStatusForUpdateAsync(SqlConnection conn, SqlTransaction tx, int id)
        => conn.ScalarAsync<string>("SELECT Status FROM dbo.StokOpname WITH (UPDLOCK, ROWLOCK) WHERE OpnameId = @Id", new { Id = id }, tx);

    public async Task<List<OpnameDetailRow>> DetailAsync(int id, SqlConnection conn = null, SqlTransaction tx = null)
    {
        const string sql = @"
SELECT d.DetailId, d.BarangId, b.SKU, b.Nama AS BarangNama, s.Nama AS Satuan, d.StokSistem, d.StokFisik, d.Alasan
FROM dbo.StokOpnameDetail d JOIN dbo.Barang b ON b.BarangId = d.BarangId JOIN dbo.Satuan s ON s.SatuanId = b.SatuanId
WHERE d.OpnameId = @Id ORDER BY b.Nama";
        static OpnameDetailRow Map(SqlDataReader r) => new()
        {
            DetailId = r.Int("DetailId"), BarangId = r.Int("BarangId"), SKU = r.Str("SKU"), BarangNama = r.Str("BarangNama"),
            Satuan = r.Str("Satuan"), StokSistem = r.Dec("StokSistem"), StokFisik = r.DecN("StokFisik"), Alasan = r.Str("Alasan")
        };
        if (conn != null) return await conn.QueryAsync(sql, Map, new { Id = id }, tx);
        await using var c = await _db.OpenAsync();
        return await c.QueryAsync(sql, Map, new { Id = id });
    }

    public Task<int> InsertAsync(SqlConnection conn, SqlTransaction tx, string nomor, int? kategoriId, string catatan)
        => conn.ScalarAsync<int>(@"
INSERT INTO dbo.StokOpname (NomorDokumen, Tanggal, KategoriId, Catatan, DibuatOleh) OUTPUT inserted.OpnameId
VALUES (@Nomor, SYSDATETIME(), @KategoriId, @Catatan, @UserId)",
            new { Nomor = nomor, KategoriId = kategoriId, Catatan = catatan, UserId = Session.UserId }, tx);

    /// <summary>Menyalin stok sistem saat ini sebagai pembanding.</summary>
    public Task<int> SnapshotAsync(SqlConnection conn, SqlTransaction tx, int opnameId, int? kategoriId)
        => conn.ExecAsync(@"
INSERT INTO dbo.StokOpnameDetail (OpnameId, BarangId, StokSistem)
SELECT @OpnameId, b.BarangId, b.StokSaatIni FROM dbo.Barang b
WHERE b.IsActive = 1 AND (@KategoriId IS NULL OR b.KategoriId = @KategoriId)",
            new { OpnameId = opnameId, KategoriId = kategoriId }, tx);

    public Task UpdateDetailAsync(SqlConnection conn, SqlTransaction tx, int detailId, decimal? fisik, string alasan)
        => conn.ExecAsync("UPDATE dbo.StokOpnameDetail SET StokFisik = @Fisik, Alasan = @Alasan WHERE DetailId = @Id",
            new { Id = detailId, Fisik = fisik, Alasan = string.IsNullOrWhiteSpace(alasan) ? null : alasan.Trim() }, tx);

    public Task SetStatusAsync(SqlConnection conn, SqlTransaction tx, int id, string status, string alasanTolak = null)
    {
        var sql = status switch
        {
            "DIAJUKAN" => "UPDATE dbo.StokOpname SET Status = 'DIAJUKAN', DiajukanOleh = @UserId, DiajukanPada = SYSDATETIME() WHERE OpnameId = @Id",
            "DISETUJUI" => "UPDATE dbo.StokOpname SET Status = 'DISETUJUI', DisetujuiOleh = @UserId, DisetujuiPada = SYSDATETIME() WHERE OpnameId = @Id",
            "DITOLAK" => "UPDATE dbo.StokOpname SET Status = 'DITOLAK', DisetujuiOleh = @UserId, DisetujuiPada = SYSDATETIME(), AlasanTolak = @Alasan WHERE OpnameId = @Id",
            "DRAFT" => "UPDATE dbo.StokOpname SET Status = 'DRAFT' WHERE OpnameId = @Id",
            _ => throw new ArgumentException("Status tidak dikenal", nameof(status))
        };
        return conn.ExecAsync(sql, new { Id = id, UserId = Session.UserId, Alasan = alasanTolak }, tx);
    }
}
