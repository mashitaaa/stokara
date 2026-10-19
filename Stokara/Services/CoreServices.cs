using Stokara.Infrastructure;
using Stokara.Models;
using Stokara.Repositories;

namespace Stokara.Services;

/// <summary>Log aktivitas (FR-29). Tidak menyimpan password atau NIK.</summary>
public sealed class ActivityService
{
    private readonly AktivitasRepository _repo;
    public ActivityService(AktivitasRepository repo) => _repo = repo;

    public async Task LogAsync(string aksi, string entitas, object entitasId = null, string keterangan = null)
    {
        try
        {
            await _repo.InsertAsync(Session.Current?.UserId, aksi, entitas, entitasId?.ToString(), keterangan);
        }
        catch (Exception ex)
        {
            Logger.Error("Gagal menulis log aktivitas", ex);
        }
    }

    public Task<List<Aktivitas>> ListAsync(DateTime dari, DateTime sampai, int? userId, string aksi)
    {
        Session.Require("melihat log aktivitas", Roles.Manajer, Roles.Pengurus);
        return _repo.ListAsync(dari, sampai, userId, aksi);
    }

    public Task<List<Aktivitas>> RecentAsync(int top) => _repo.RecentAsync(top);
    public Task<List<string>> ListAksiAsync() => _repo.ListAksiAsync();
}

/// <summary>Pengaturan gerai dan parameter (BR-19).</summary>
public sealed class SettingsService
{
    private readonly MasterRepository _repo;
    private readonly ActivityService _log;
    private Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);

    public SettingsService(MasterRepository repo, ActivityService log)
    {
        _repo = repo;
        _log = log;
    }

    public async Task RefreshAsync() => _cache = await _repo.GetSettingsAsync();

    public string Get(string key, string fallback = "") => _cache.TryGetValue(key, out var v) && v != null ? v : fallback;

    public int GetInt(string key, int fallback) => int.TryParse(Get(key), out var v) ? v : fallback;

    public string NamaGerai => Get("NamaGerai", "Gerai Kopdes Merah Putih");
    public string AlamatGerai => Get("AlamatGerai");
    public int AmbangKedaluwarsaHari => GetInt("AmbangKedaluwarsaHari", 30);
    public int UkuranHalaman => Math.Clamp(GetInt("UkuranHalaman", 25), 10, 200);

    /// <summary>BULANAN (default, A-04) atau TAHUNAN.</summary>
    public string JenisPeriodeKuota => Get("PeriodeKuota", "BULANAN");

    public string PeriodeKuota(DateTime tanggal) =>
        JenisPeriodeKuota == "TAHUNAN" ? tanggal.ToString("yyyy") : tanggal.ToString("yyyy-MM");

    public string PeriodeSekarang => PeriodeKuota(DateTime.Today);

    public async Task SaveAsync(Dictionary<string, string> values)
    {
        Session.Require("mengubah pengaturan", Roles.Manajer);
        foreach (var kv in values)
        {
            if (Get(kv.Key) == kv.Value) continue;
            await _repo.SetSettingAsync(kv.Key, kv.Value);
            await _log.LogAsync("UBAH_PENGATURAN", "Pengaturan", kv.Key, $"{kv.Key} = {kv.Value}");
        }
        await RefreshAsync();
    }
}

/// <summary>Login, logout, dan penguncian sementara setelah gagal berulang (FR-01).</summary>
public sealed class AuthService
{
    private readonly UserRepository _users;
    private readonly ActivityService _log;
    private readonly AppConfig _config;

    public AuthService(UserRepository users, ActivityService log, AppConfig config)
    {
        _users = users;
        _log = log;
        _config = config;
    }

    public async Task<User> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            throw new BusinessException("Isi nama pengguna dan kata sandi.");

        var rec = await _users.GetByUsernameAsync(username.Trim());
        // Pesan dibuat sama untuk username/password salah agar tidak membocorkan akun yang ada.
        const string invalid = "Nama pengguna atau kata sandi salah.";
        if (rec == null) throw new BusinessException(invalid);

        if (rec.LockedUntil.HasValue && rec.LockedUntil.Value > DateTime.Now)
        {
            var menit = Math.Max(1, (int)Math.Ceiling((rec.LockedUntil.Value - DateTime.Now).TotalMinutes));
            throw new BusinessException($"Akun dikunci sementara karena terlalu banyak percobaan gagal. Coba lagi dalam {menit} menit.");
        }

        if (!PasswordHasher.Verify(password, rec.PasswordHash))
        {
            await _users.RecordLoginFailureAsync(rec.User.UserId, _config.LoginMaxAttempts, _config.LoginLockMinutes);
            await _log.LogAsync("LOGIN_GAGAL", "Users", rec.User.UserId, "Kata sandi salah");
            throw new BusinessException(invalid);
        }

        if (!rec.User.IsActive)
            throw new BusinessException("Akun ini sudah dinonaktifkan. Hubungi Manajer Gerai.");

        await _users.RecordLoginSuccessAsync(rec.User.UserId);
        Session.Start(rec.User);
        await _log.LogAsync("LOGIN", "Users", rec.User.UserId);
        return rec.User;
    }

    public async Task LogoutAsync()
    {
        if (Session.Current != null) await _log.LogAsync("LOGOUT", "Users", Session.Current.UserId);
        Session.End();
    }

    public async Task ChangeOwnPasswordAsync(string oldPassword, string newPassword)
    {
        var rec = await _users.GetAsync(Session.UserId);
        if (!PasswordHasher.Verify(oldPassword ?? "", rec.PasswordHash))
            throw new BusinessException("Kata sandi lama tidak sesuai.");
        UserService.ValidatePassword(newPassword);
        await _users.SetPasswordAsync(rec.User.UserId, PasswordHasher.Hash(newPassword));
        await _log.LogAsync("UBAH_PASSWORD", "Users", rec.User.UserId);
    }

    public async Task SetThemeAsync(string theme)
    {
        if (Session.Current == null) return;
        Session.Current.Tema = theme;
        await _users.SetThemeAsync(Session.Current.UserId, theme);
    }
}

/// <summary>Manajemen pengguna oleh Manajer (FR-03).</summary>
public sealed class UserService
{
    private readonly UserRepository _users;
    private readonly ActivityService _log;

    public UserService(UserRepository users, ActivityService log)
    {
        _users = users;
        _log = log;
    }

    public static void ValidatePassword(string pwd)
    {
        if (string.IsNullOrEmpty(pwd) || pwd.Length < 6)
            throw new BusinessException("Kata sandi minimal 6 karakter.");
    }

    public Task<List<User>> ListAsync()
    {
        Session.Require("melihat daftar pengguna", Roles.Manajer);
        return _users.ListAsync();
    }

    public async Task<int> SaveAsync(User u, string newPassword)
    {
        Session.Require("mengelola pengguna", Roles.Manajer);
        if (string.IsNullOrWhiteSpace(u.Username) || string.IsNullOrWhiteSpace(u.Nama))
            throw new BusinessException("Nama pengguna dan nama lengkap wajib diisi.");
        if (!Roles.All.Contains(u.Role)) throw new BusinessException("Pilih peran pengguna.");
        if (await _users.UsernameExistsAsync(u.Username.Trim(), u.UserId))
            throw new BusinessException($"Nama pengguna '{u.Username}' sudah dipakai.");

        if (u.UserId == 0)
        {
            ValidatePassword(newPassword);
            u.Username = u.Username.Trim();
            var id = await _users.InsertAsync(u, PasswordHasher.Hash(newPassword));
            await _log.LogAsync("TAMBAH", "Users", id, $"{u.Username} ({u.Role})");
            return id;
        }

        if (u.UserId == Session.UserId && (!u.IsActive || u.Role != Roles.Manajer))
            throw new BusinessException("Anda tidak dapat menonaktifkan atau menurunkan peran akun sendiri.");
        await _users.UpdateAsync(u);
        await _log.LogAsync("UBAH", "Users", u.UserId, $"Peran {u.Role}, {(u.IsActive ? "aktif" : "nonaktif")}");
        return u.UserId;
    }

    public async Task ResetPasswordAsync(int userId, string newPassword)
    {
        Session.Require("mereset kata sandi", Roles.Manajer);
        ValidatePassword(newPassword);
        await _users.SetPasswordAsync(userId, PasswordHasher.Hash(newPassword));
        await _log.LogAsync("RESET_PASSWORD", "Users", userId);
    }
}
