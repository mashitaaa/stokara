using Stokara.Models;

namespace Stokara.Infrastructure;

/// <summary>Sesi pengguna yang sedang login dan pemeriksaan otorisasi (FR-02).</summary>
public static class Session
{
    public static User Current { get; private set; }

    public static event Action Changed;

    public static void Start(User user)
    {
        Current = user;
        Changed?.Invoke();
    }

    public static void End()
    {
        Current = null;
        Changed?.Invoke();
    }

    public static bool IsLoggedIn => Current != null;

    public static int UserId => Current?.UserId ?? throw new AccessDeniedException("melakukan aksi tanpa login");

    public static bool Is(params string[] roles) => Current != null && roles.Contains(Current.Role);

    /// <summary>Melempar AccessDeniedException bila peran tidak termasuk daftar yang diizinkan.</summary>
    public static void Require(string action, params string[] roles)
    {
        if (Current == null || !roles.Contains(Current.Role))
            throw new AccessDeniedException(action);
    }

    /// <summary>NIK lengkap hanya untuk Manajer (NFR-06).</summary>
    public static bool CanSeeFullNik => Is(Roles.Manajer);

    public static string NikForDisplay(string nik) => CanSeeFullNik ? nik : Fmt.MaskNik(nik);
}
