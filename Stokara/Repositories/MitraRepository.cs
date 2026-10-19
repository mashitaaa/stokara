using Microsoft.Data.SqlClient;
using Stokara.Infrastructure;
using Stokara.Models;

namespace Stokara.Repositories;

/// <summary>Anggota dan penerima subsidi.</summary>
public sealed class MitraRepository
{
    private readonly Db _db;
    public MitraRepository(Db db) => _db = db;

    // ---------------- Anggota ----------------
    private static Anggota MapAnggota(SqlDataReader r)
    {
        var nik = r.Str("NIK");
        return new Anggota
        {
            AnggotaId = r.Int("AnggotaId"),
            NomorAnggota = r.Str("NomorAnggota"),
            NIK = nik,
            NikTampil = Session.NikForDisplay(nik),
            Nama = r.Str("Nama"),
            Alamat = r.Str("Alamat"),
            Dusun = r.Str("Dusun"),
            Telepon = r.Str("Telepon"),
            Status = r.Str("Status"),
            TanggalBergabung = r.Date("TanggalBergabung")
        };
    }

    public async Task<List<Anggota>> ListAnggotaAsync(string keyword = null, bool activeOnly = false, int top = 500)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync($@"
SELECT TOP ({top}) AnggotaId, NomorAnggota, NIK, Nama, Alamat, Dusun, Telepon, Status, TanggalBergabung
FROM dbo.Anggota
WHERE (@Kw IS NULL OR Nama LIKE @Kw OR NomorAnggota LIKE @Kw OR NIK LIKE @Kw)
  AND (@ActiveOnly = 0 OR Status = 'AKTIF')
ORDER BY Nama", MapAnggota,
            new { Kw = string.IsNullOrWhiteSpace(keyword) ? null : "%" + keyword.Trim() + "%", ActiveOnly = activeOnly });
    }

    public async Task<Anggota> GetAnggotaAsync(int id, SqlConnection conn = null, SqlTransaction tx = null)
    {
        const string sql = @"SELECT AnggotaId, NomorAnggota, NIK, Nama, Alamat, Dusun, Telepon, Status, TanggalBergabung
FROM dbo.Anggota WHERE AnggotaId = @Id";
        if (conn != null) return await conn.QuerySingleAsync(sql, MapAnggota, new { Id = id }, tx);
        await using var c = await _db.OpenAsync();
        return await c.QuerySingleAsync(sql, MapAnggota, new { Id = id });
    }

    public async Task<bool> AnggotaExistsAsync(string column, string value, int excludeId)
    {
        if (column != "NIK" && column != "NomorAnggota") throw new ArgumentException("Kolom tidak dikenal", nameof(column));
        await using var conn = await _db.OpenAsync();
        return await conn.ScalarAsync<int>($"SELECT COUNT(*) FROM dbo.Anggota WHERE {column} = @V AND AnggotaId <> @Id",
            new { V = value, Id = excludeId }) > 0;
    }

    public async Task<string> NextNomorAnggotaAsync()
    {
        await using var conn = await _db.OpenAsync();
        var max = await conn.ScalarAsync<int?>(@"
SELECT MAX(TRY_CAST(SUBSTRING(NomorAnggota, 5, 10) AS INT)) FROM dbo.Anggota WHERE NomorAnggota LIKE 'AGT-%'") ?? 0;
        return $"AGT-{max + 1:0000}";
    }

    public async Task<int> SaveAnggotaAsync(Anggota a)
    {
        var args = new { a.AnggotaId, a.NomorAnggota, a.NIK, a.Nama, a.Alamat, a.Dusun, a.Telepon, a.Status, a.TanggalBergabung };
        await using var conn = await _db.OpenAsync();
        if (a.AnggotaId == 0)
            return await conn.ScalarAsync<int>(@"
INSERT INTO dbo.Anggota (NomorAnggota, NIK, Nama, Alamat, Dusun, Telepon, Status, TanggalBergabung)
OUTPUT inserted.AnggotaId VALUES (@NomorAnggota, @NIK, @Nama, @Alamat, @Dusun, @Telepon, @Status, @TanggalBergabung)", args);
        await conn.ExecAsync(@"
UPDATE dbo.Anggota SET NomorAnggota = @NomorAnggota, NIK = @NIK, Nama = @Nama, Alamat = @Alamat, Dusun = @Dusun,
       Telepon = @Telepon, Status = @Status, TanggalBergabung = @TanggalBergabung
WHERE AnggotaId = @AnggotaId", args);
        return a.AnggotaId;
    }

    // ---------------- Penerima subsidi ----------------
    private static PenerimaSubsidi MapPenerima(SqlDataReader r)
    {
        var nik = r.Str("NIK");
        return new PenerimaSubsidi
        {
            PenerimaId = r.Int("PenerimaId"),
            NIK = nik,
            NikTampil = Session.NikForDisplay(nik),
            Nama = r.Str("Nama"),
            JenisPenerima = r.Str("JenisPenerima"),
            Dusun = r.Str("Dusun"),
            AnggotaId = r.IntN("AnggotaId"),
            Status = r.Str("Status")
        };
    }

    public async Task<List<PenerimaSubsidi>> ListPenerimaAsync(string keyword = null, bool activeOnly = false, int top = 500)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync($@"
SELECT TOP ({top}) PenerimaId, NIK, Nama, JenisPenerima, Dusun, AnggotaId, Status
FROM dbo.PenerimaSubsidi
WHERE (@Kw IS NULL OR Nama LIKE @Kw OR NIK LIKE @Kw) AND (@ActiveOnly = 0 OR Status = 'AKTIF')
ORDER BY Nama", MapPenerima,
            new { Kw = string.IsNullOrWhiteSpace(keyword) ? null : "%" + keyword.Trim() + "%", ActiveOnly = activeOnly });
    }

    public async Task<PenerimaSubsidi> GetPenerimaAsync(int id, SqlConnection conn = null, SqlTransaction tx = null)
    {
        const string sql = "SELECT PenerimaId, NIK, Nama, JenisPenerima, Dusun, AnggotaId, Status FROM dbo.PenerimaSubsidi WHERE PenerimaId = @Id";
        if (conn != null) return await conn.QuerySingleAsync(sql, MapPenerima, new { Id = id }, tx);
        await using var c = await _db.OpenAsync();
        return await c.QuerySingleAsync(sql, MapPenerima, new { Id = id });
    }

    public async Task<bool> PenerimaNikExistsAsync(string nik, int excludeId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.PenerimaSubsidi WHERE NIK = @Nik AND PenerimaId <> @Id",
            new { Nik = nik, Id = excludeId }) > 0;
    }

    public async Task<int> SavePenerimaAsync(PenerimaSubsidi p)
    {
        var args = new { p.PenerimaId, p.NIK, p.Nama, p.JenisPenerima, p.Dusun, p.AnggotaId, p.Status };
        await using var conn = await _db.OpenAsync();
        if (p.PenerimaId == 0)
            return await conn.ScalarAsync<int>(@"
INSERT INTO dbo.PenerimaSubsidi (NIK, Nama, JenisPenerima, Dusun, AnggotaId, Status)
OUTPUT inserted.PenerimaId VALUES (@NIK, @Nama, @JenisPenerima, @Dusun, @AnggotaId, @Status)", args);
        await conn.ExecAsync(@"
UPDATE dbo.PenerimaSubsidi SET NIK = @NIK, Nama = @Nama, JenisPenerima = @JenisPenerima, Dusun = @Dusun,
       AnggotaId = @AnggotaId, Status = @Status
WHERE PenerimaId = @PenerimaId", args);
        return p.PenerimaId;
    }
}
