using Stokara.Infrastructure;
using Stokara.Models;

namespace Stokara.Repositories;

/// <summary>Kategori, satuan, pemasok, penitip, dan pengaturan.</summary>
public sealed class MasterRepository
{
    private readonly Db _db;
    public MasterRepository(Db db) => _db = db;

    // ---------------- Kategori & Satuan ----------------
    private static string LookupTable(string kind) => kind switch
    {
        "Kategori" => "Kategori",
        "Satuan" => "Satuan",
        _ => throw new ArgumentException("Jenis data tidak dikenal", nameof(kind))
    };

    public async Task<List<LookupItem>> ListLookupAsync(string kind, bool activeOnly = true)
    {
        var t = LookupTable(kind);
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync(
            $"SELECT {t}Id AS Id, Nama, IsActive FROM dbo.{t}" + (activeOnly ? " WHERE IsActive = 1" : "") + " ORDER BY Nama",
            r => new LookupItem { Id = r.Int("Id"), Nama = r.Str("Nama"), IsActive = r.Bool("IsActive") });
    }

    public async Task<bool> LookupExistsAsync(string kind, string nama, int excludeId)
    {
        var t = LookupTable(kind);
        await using var conn = await _db.OpenAsync();
        return await conn.ScalarAsync<int>($"SELECT COUNT(*) FROM dbo.{t} WHERE Nama = @Nama AND {t}Id <> @Id",
            new { Nama = nama, Id = excludeId }) > 0;
    }

    public async Task<int> SaveLookupAsync(string kind, LookupItem item)
    {
        var t = LookupTable(kind);
        await using var conn = await _db.OpenAsync();
        if (item.Id == 0)
            return await conn.ScalarAsync<int>($"INSERT INTO dbo.{t} (Nama) OUTPUT inserted.{t}Id VALUES (@Nama)", new { item.Nama });
        await conn.ExecAsync($"UPDATE dbo.{t} SET Nama = @Nama WHERE {t}Id = @Id", new { item.Nama, item.Id });
        return item.Id;
    }

    public async Task SetLookupActiveAsync(string kind, int id, bool active)
    {
        var t = LookupTable(kind);
        await using var conn = await _db.OpenAsync();
        await conn.ExecAsync($"UPDATE dbo.{t} SET IsActive = @Active WHERE {t}Id = @Id", new { Id = id, Active = active });
    }

    // ---------------- Pemasok ----------------
    public async Task<List<Pemasok>> ListPemasokAsync(string keyword = null, bool activeOnly = false)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync(@"
SELECT PemasokId, Kode, Nama, Jenis, Telepon, Alamat, IsActive FROM dbo.Pemasok
WHERE (@Kw IS NULL OR Nama LIKE @Kw OR Kode LIKE @Kw) AND (@ActiveOnly = 0 OR IsActive = 1)
ORDER BY Nama",
            r => new Pemasok
            {
                PemasokId = r.Int("PemasokId"), Kode = r.Str("Kode"), Nama = r.Str("Nama"), Jenis = r.Str("Jenis"),
                Telepon = r.Str("Telepon"), Alamat = r.Str("Alamat"), IsActive = r.Bool("IsActive")
            },
            new { Kw = string.IsNullOrWhiteSpace(keyword) ? null : "%" + keyword.Trim() + "%", ActiveOnly = activeOnly });
    }

    public async Task<bool> PemasokKodeExistsAsync(string kode, int excludeId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Pemasok WHERE Kode = @Kode AND PemasokId <> @Id",
            new { Kode = kode, Id = excludeId }) > 0;
    }

    public async Task<int> SavePemasokAsync(Pemasok p)
    {
        await using var conn = await _db.OpenAsync();
        if (p.PemasokId == 0)
            return await conn.ScalarAsync<int>(@"
INSERT INTO dbo.Pemasok (Kode, Nama, Jenis, Telepon, Alamat) OUTPUT inserted.PemasokId
VALUES (@Kode, @Nama, @Jenis, @Telepon, @Alamat)", new { p.Kode, p.Nama, p.Jenis, p.Telepon, p.Alamat });
        await conn.ExecAsync(@"
UPDATE dbo.Pemasok SET Kode = @Kode, Nama = @Nama, Jenis = @Jenis, Telepon = @Telepon, Alamat = @Alamat
WHERE PemasokId = @PemasokId", new { p.PemasokId, p.Kode, p.Nama, p.Jenis, p.Telepon, p.Alamat });
        return p.PemasokId;
    }

    public async Task SetPemasokActiveAsync(int id, bool active)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecAsync("UPDATE dbo.Pemasok SET IsActive = @Active WHERE PemasokId = @Id", new { Id = id, Active = active });
    }

    // ---------------- Penitip ----------------
    public async Task<List<Penitip>> ListPenitipAsync(string keyword = null, bool activeOnly = false)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync(@"
SELECT PenitipId, Nama, Telepon, Alamat, IsActive FROM dbo.Penitip
WHERE (@Kw IS NULL OR Nama LIKE @Kw) AND (@ActiveOnly = 0 OR IsActive = 1)
ORDER BY Nama",
            r => new Penitip
            {
                PenitipId = r.Int("PenitipId"), Nama = r.Str("Nama"), Telepon = r.Str("Telepon"),
                Alamat = r.Str("Alamat"), IsActive = r.Bool("IsActive")
            },
            new { Kw = string.IsNullOrWhiteSpace(keyword) ? null : "%" + keyword.Trim() + "%", ActiveOnly = activeOnly });
    }

    public async Task<int> SavePenitipAsync(Penitip p)
    {
        await using var conn = await _db.OpenAsync();
        if (p.PenitipId == 0)
            return await conn.ScalarAsync<int>(@"
INSERT INTO dbo.Penitip (Nama, Telepon, Alamat) OUTPUT inserted.PenitipId VALUES (@Nama, @Telepon, @Alamat)",
                new { p.Nama, p.Telepon, p.Alamat });
        await conn.ExecAsync("UPDATE dbo.Penitip SET Nama = @Nama, Telepon = @Telepon, Alamat = @Alamat WHERE PenitipId = @PenitipId",
            new { p.PenitipId, p.Nama, p.Telepon, p.Alamat });
        return p.PenitipId;
    }

    public async Task SetPenitipActiveAsync(int id, bool active)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecAsync("UPDATE dbo.Penitip SET IsActive = @Active WHERE PenitipId = @Id", new { Id = id, Active = active });
    }

    // ---------------- Pengaturan ----------------
    public async Task<Dictionary<string, string>> GetSettingsAsync()
    {
        await using var conn = await _db.OpenAsync();
        var rows = await conn.QueryAsync("SELECT Kunci, Nilai FROM dbo.Pengaturan",
            r => new KeyValuePair<string, string>(r.Str("Kunci"), r.Str("Nilai")));
        return rows.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
    }

    public async Task SetSettingAsync(string key, string value)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecAsync(@"
UPDATE dbo.Pengaturan SET Nilai = @Nilai WHERE Kunci = @Kunci;
IF @@ROWCOUNT = 0 INSERT INTO dbo.Pengaturan (Kunci, Nilai) VALUES (@Kunci, @Nilai);",
            new { Kunci = key, Nilai = value });
    }
}
