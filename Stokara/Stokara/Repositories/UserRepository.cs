using Microsoft.Data.SqlClient;
using Stokara.Infrastructure;
using Stokara.Models;

namespace Stokara.Repositories;

public sealed class UserRecord
{
    public User User { get; set; }
    public string PasswordHash { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntil { get; set; }
}

public sealed class UserRepository
{
    private readonly Db _db;
    public UserRepository(Db db) => _db = db;

    private const string Select = @"SELECT UserId, Username, PasswordHash, Nama, Role, Tema, IsActive, FailedLoginCount,
       LockedUntil, CreatedAt, LastLoginAt FROM dbo.Users";

    private static UserRecord Map(SqlDataReader r) => new()
    {
        User = new User
        {
            UserId = r.Int("UserId"),
            Username = r.Str("Username"),
            Nama = r.Str("Nama"),
            Role = r.Str("Role"),
            Tema = r.Str("Tema"),
            IsActive = r.Bool("IsActive"),
            CreatedAt = r.Date("CreatedAt"),
            LastLoginAt = r.DateN("LastLoginAt")
        },
        PasswordHash = r.Str("PasswordHash"),
        FailedLoginCount = r.Int("FailedLoginCount"),
        LockedUntil = r.DateN("LockedUntil")
    };

    public async Task<UserRecord> GetByUsernameAsync(string username)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleAsync(Select + " WHERE Username = @U", Map, new { U = username });
    }

    public async Task<UserRecord> GetAsync(int id)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QuerySingleAsync(Select + " WHERE UserId = @Id", Map, new { Id = id });
    }

    public async Task<List<User>> ListAsync()
    {
        await using var conn = await _db.OpenAsync();
        return (await conn.QueryAsync(Select + " ORDER BY Nama", Map)).Select(x => x.User).ToList();
    }

    public async Task<int> CountAsync()
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Users");
    }

    public async Task<bool> UsernameExistsAsync(string username, int excludeId)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ScalarAsync<int>("SELECT COUNT(*) FROM dbo.Users WHERE Username = @U AND UserId <> @Id",
            new { U = username, Id = excludeId }) > 0;
    }

    public async Task<int> InsertAsync(User u, string passwordHash)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.ScalarAsync<int>(@"
INSERT INTO dbo.Users (Username, PasswordHash, Nama, Role, Tema, IsActive) OUTPUT inserted.UserId
VALUES (@Username, @Hash, @Nama, @Role, @Tema, @IsActive)",
            new { u.Username, Hash = passwordHash, u.Nama, u.Role, Tema = u.Tema ?? "Light", u.IsActive });
    }

    public async Task UpdateAsync(User u)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecAsync("UPDATE dbo.Users SET Nama = @Nama, Role = @Role, IsActive = @IsActive WHERE UserId = @UserId",
            new { u.Nama, u.Role, u.IsActive, u.UserId });
    }

    public async Task SetPasswordAsync(int userId, string hash)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecAsync("UPDATE dbo.Users SET PasswordHash = @Hash, FailedLoginCount = 0, LockedUntil = NULL WHERE UserId = @Id",
            new { Id = userId, Hash = hash });
    }

    public async Task SetThemeAsync(int userId, string theme)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecAsync("UPDATE dbo.Users SET Tema = @Tema WHERE UserId = @Id", new { Id = userId, Tema = theme });
    }

    public async Task RecordLoginSuccessAsync(int userId)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecAsync("UPDATE dbo.Users SET FailedLoginCount = 0, LockedUntil = NULL, LastLoginAt = SYSDATETIME() WHERE UserId = @Id",
            new { Id = userId });
    }

    public async Task RecordLoginFailureAsync(int userId, int maxAttempts, int lockMinutes)
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecAsync(@"
UPDATE dbo.Users SET
    FailedLoginCount = FailedLoginCount + 1,
    LockedUntil = CASE WHEN FailedLoginCount + 1 >= @Max THEN DATEADD(MINUTE, @Lock, SYSDATETIME()) ELSE LockedUntil END
WHERE UserId = @Id;
UPDATE dbo.Users SET FailedLoginCount = 0 WHERE UserId = @Id AND LockedUntil IS NOT NULL AND FailedLoginCount >= @Max;",
            new { Id = userId, Max = maxAttempts, Lock = lockMinutes });
    }
}

public sealed class AktivitasRepository
{
    private readonly Db _db;
    public AktivitasRepository(Db db) => _db = db;

    public async Task InsertAsync(int? userId, string aksi, string entitas, string entitasId, string keterangan,
        SqlConnection conn = null, SqlTransaction tx = null)
    {
        const string sql = @"INSERT INTO dbo.Aktivitas (UserId, JenisAksi, Entitas, EntitasId, Keterangan)
VALUES (@UserId, @Aksi, @Entitas, @EntitasId, @Keterangan)";
        var args = new
        {
            UserId = userId,
            Aksi = aksi,
            Entitas = entitas,
            EntitasId = entitasId,
            Keterangan = keterangan != null && keterangan.Length > 400 ? keterangan[..400] : keterangan
        };
        if (conn != null)
        {
            await conn.ExecAsync(sql, args, tx);
            return;
        }
        await using var c = await _db.OpenAsync();
        await c.ExecAsync(sql, args);
    }

    public async Task<List<Aktivitas>> ListAsync(DateTime dari, DateTime sampai, int? userId, string aksi, int top = 500)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync($@"
SELECT TOP ({top}) a.AktivitasId, ISNULL(u.Nama, '-') AS Pengguna, a.JenisAksi, a.Entitas, a.EntitasId, a.Keterangan, a.Waktu
FROM dbo.Aktivitas a LEFT JOIN dbo.Users u ON u.UserId = a.UserId
WHERE a.Waktu >= @Dari AND a.Waktu < @Sampai
  AND (@UserId IS NULL OR a.UserId = @UserId)
  AND (@Aksi IS NULL OR a.JenisAksi = @Aksi)
ORDER BY a.Waktu DESC",
            Map, new { Dari = dari.Date, Sampai = sampai.Date.AddDays(1), UserId = userId, Aksi = aksi });
    }

    public async Task<List<Aktivitas>> RecentAsync(int top)
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync($@"
SELECT TOP ({top}) a.AktivitasId, ISNULL(u.Nama, '-') AS Pengguna, a.JenisAksi, a.Entitas, a.EntitasId, a.Keterangan, a.Waktu
FROM dbo.Aktivitas a LEFT JOIN dbo.Users u ON u.UserId = a.UserId
WHERE a.JenisAksi NOT IN ('LOGIN','LOGOUT')
ORDER BY a.Waktu DESC", Map);
    }

    public async Task<List<string>> ListAksiAsync()
    {
        await using var conn = await _db.OpenAsync();
        return await conn.QueryAsync("SELECT DISTINCT JenisAksi FROM dbo.Aktivitas ORDER BY JenisAksi", r => r.Str("JenisAksi"));
    }

    private static Aktivitas Map(SqlDataReader r) => new()
    {
        AktivitasId = r.Long("AktivitasId"),
        Pengguna = r.Str("Pengguna"),
        JenisAksi = r.Str("JenisAksi"),
        Entitas = r.Str("Entitas"),
        EntitasId = r.Str("EntitasId"),
        Keterangan = r.Str("Keterangan"),
        Waktu = r.Date("Waktu")
    };
}
