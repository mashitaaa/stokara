using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Stokara.Infrastructure;

/// <summary>Kesalahan aturan bisnis - pesannya aman dan dapat dibaca pengguna awam.</summary>
public sealed class BusinessException : Exception
{
    public BusinessException(string message) : base(message) { }
}

/// <summary>Aksi ditolak karena peran pengguna tidak berwenang (divalidasi di service, FR-02).</summary>
public sealed class AccessDeniedException : Exception
{
    public string Action { get; }
    public AccessDeniedException(string action)
        : base($"Akses ditolak: Anda tidak berwenang untuk {action}.") { Action = action; }
}

/// <summary>Konfigurasi dari appsettings.json (connection string tidak ditulis di kode, NFR-05).</summary>
public sealed class AppConfig
{
    public string ConnectionString { get; private set; }
    public bool SeedDemoData { get; private set; } = true;
    public int LoginMaxAttempts { get; private set; } = 5;
    public int LoginLockMinutes { get; private set; } = 5;
    public int IdleLogoutMinutes { get; private set; }

    public static AppConfig Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
            throw new InvalidOperationException("File appsettings.json tidak ditemukan di folder aplikasi.");

        using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });
        var root = doc.RootElement;
        var cfg = new AppConfig
        {
            ConnectionString = root.GetProperty("ConnectionStrings").GetProperty("Stokara").GetString()
        };
        if (root.TryGetProperty("SeedDemoData", out var s)) cfg.SeedDemoData = s.GetBoolean();
        if (root.TryGetProperty("LoginMaxAttempts", out var m)) cfg.LoginMaxAttempts = m.GetInt32();
        if (root.TryGetProperty("LoginLockMinutes", out var l)) cfg.LoginLockMinutes = l.GetInt32();
        if (root.TryGetProperty("IdleLogoutMinutes", out var i)) cfg.IdleLogoutMinutes = i.GetInt32();
        return cfg;
    }
}

/// <summary>Log teknis ke file (NFR-07). Tidak pernah menulis password atau NIK.</summary>
public static class Logger
{
    private static readonly object Gate = new();

    public static string LogFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Stokara", "logs");

    public static void Error(string context, Exception ex) => Write("ERROR", $"{context}: {ex}");
    public static void Info(string message) => Write("INFO", message);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogFolder);
                var file = Path.Combine(LogFolder, $"stokara-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging tidak boleh membuat aplikasi gagal.
        }
    }
}

/// <summary>Hash password PBKDF2-SHA256 dengan salt acak (NFR-05).</summary>
public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"PBKDF2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string stored)
    {
        if (string.IsNullOrEmpty(stored)) return false;
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "PBKDF2") return false;
        if (!int.TryParse(parts[1], out var iterations)) return false;
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

/// <summary>Format tampilan berbahasa Indonesia (NFR-10).</summary>
public static class Fmt
{
    public static readonly CultureInfo Id = CultureInfo.GetCultureInfo("id-ID");

    public static string Rp(decimal v) => "Rp " + v.ToString("N0", Id);
    public static string Num(decimal v) => v.ToString("#,0.##", Id);
    public static string Tgl(DateTime v) => v.ToString("dd/MM/yyyy", Id);
    public static string TglJam(DateTime v) => v.ToString("dd/MM/yyyy HH:mm", Id);

    /// <summary>Menyamarkan NIK untuk peran yang tidak berwenang (NFR-06): 3578••••••••0001.</summary>
    public static string MaskNik(string nik)
    {
        if (string.IsNullOrWhiteSpace(nik) || nik.Length < 8) return nik;
        return nik[..4] + new string('•', nik.Length - 8) + nik[^4..];
    }

    public static string JenisPenerimaLabel(string kode) => kode switch
    {
        "PETANI" => "Petani",
        "RUMAH_TANGGA" => "Rumah Tangga",
        "USAHA_MIKRO" => "Usaha Mikro",
        "LAINNYA" => "Lainnya",
        _ => kode
    };

    public static string SumberLabel(string kode) => kode switch
    {
        "STOK_AWAL" => "Stok Awal",
        "PEMBELIAN" => "Pembelian",
        "SUBSIDI" => "Pasokan Subsidi",
        "KONSINYASI" => "Konsinyasi (Titipan)",
        "RETUR_PEMASOK" => "Retur ke Pemasok",
        "RETUR_PENITIP" => "Retur ke Penitip",
        "RUSAK" => "Rusak",
        "KEDALUWARSA" => "Kedaluwarsa",
        "HILANG" => "Hilang",
        _ => kode
    };

    public static string MutasiLabel(string kode) => kode switch
    {
        "MASUK_STOK_AWAL" => "Masuk: Stok Awal",
        "MASUK_PEMBELIAN" => "Masuk: Pembelian",
        "MASUK_SUBSIDI" => "Masuk: Subsidi",
        "MASUK_KONSINYASI" => "Masuk: Titipan",
        "KELUAR_JUAL" => "Keluar: Penjualan",
        "KELUAR_SUBSIDI" => "Keluar: Penyaluran",
        "KELUAR_RETUR" => "Keluar: Retur",
        "KELUAR_RUSAK" => "Keluar: Rusak/Hilang",
        "VOID" => "Pembatalan",
        "PENYESUAIAN" => "Penyesuaian Opname",
        _ => kode
    };
}
