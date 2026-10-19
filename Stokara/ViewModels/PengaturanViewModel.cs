using System.Collections.ObjectModel;
using System.Windows.Input;
using Stokara.Helpers;
using Stokara.Infrastructure;
using Stokara.Models;

namespace Stokara.ViewModels;

/// <summary>Pengaturan: profil &amp; tema, kategori &amp; satuan, pengguna, parameter, log aktivitas, backup/restore.</summary>
public sealed class PengaturanViewModel : ViewModelBase
{
    private int _tab;

    public PengaturanViewModel()
    {
        ThemeCommand = new AsyncRelayCommand(async p =>
        {
            var theme = p as string;
            ThemeService.Apply(theme);
            await AppServices.Auth.SetThemeAsync(theme);
            OnPropertyChanged(nameof(IsDark));
            OnPropertyChanged(nameof(IsLight));
        });

        // Kategori & satuan
        NewLookupCommand = new RelayCommand(p => { if (p as string == "Kategori") EditKategori = new LookupItem(); else EditSatuan = new LookupItem(); });
        SaveLookupCommand = new AsyncRelayCommand(async p => await SaveLookupAsync(p as string));
        ToggleLookupCommand = new AsyncRelayCommand(async p => await ToggleLookupAsync(p as string));

        // Pengguna
        NewUserCommand = new RelayCommand(() => { SelectedUser = null; EditUser = new User { Role = Roles.Kasir, IsActive = true }; });
        SaveUserCommand = new AsyncRelayCommand(SaveUserAsync, () => EditUser != null);
        ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync, () => SelectedUser != null);

        // Parameter
        SaveParameterCommand = new AsyncRelayCommand(SaveParameterAsync);
        SaveHetCommand = new AsyncRelayCommand(SaveHetAsync, () => HetBarang != null);

        // Log
        LoadLogCommand = new AsyncRelayCommand(LoadLogAsync);

        // Backup
        BackupCommand = new AsyncRelayCommand(BackupAsync);
        RestoreCommand = new AsyncRelayCommand(RestoreAsync);
    }

    public bool IsManajer => Session.Is(Roles.Manajer);
    public bool CanSeeLog => Session.Is(Roles.Manajer, Roles.Pengurus);

    public int Tab
    {
        get => _tab;
        set { if (SetProperty(ref _tab, value)) _ = SafeLoadTab(); }
    }

    // ================= Profil & tema =================
    public ICommand ThemeCommand { get; }
    public string Nama => Session.Current?.Nama;
    public string Username => Session.Current?.Username;
    public string Peran => Session.Current?.RoleLabel;
    public bool IsDark => ThemeService.Current == "Dark";
    public bool IsLight => !IsDark;
    public string LogFolder => Logger.LogFolder;

    /// <summary>Dipanggil View dengan isi PasswordBox (password tidak disimpan di properti).</summary>
    public async Task<bool> ChangePasswordAsync(string lama, string baru, string ulang)
    {
        if (baru != ulang)
        {
            ToastService.Warning("Kata sandi baru dan ulangannya tidak sama.");
            return false;
        }
        try
        {
            await AppServices.Auth.ChangeOwnPasswordAsync(lama, baru);
            ToastService.Success("Kata sandi berhasil diubah.");
            return true;
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex);
            return false;
        }
    }

    // ================= Kategori & satuan =================
    private LookupItem _selKategori, _editKategori, _selSatuan, _editSatuan;
    public ObservableCollection<LookupItem> KategoriList { get; } = new();
    public ObservableCollection<LookupItem> SatuanList { get; } = new();
    public ICommand NewLookupCommand { get; }
    public ICommand SaveLookupCommand { get; }
    public ICommand ToggleLookupCommand { get; }

    public LookupItem SelectedKategori
    {
        get => _selKategori;
        set { if (SetProperty(ref _selKategori, value) && value != null) EditKategori = new LookupItem { Id = value.Id, Nama = value.Nama, IsActive = value.IsActive }; }
    }

    public LookupItem EditKategori { get => _editKategori; set => SetProperty(ref _editKategori, value); }

    public LookupItem SelectedSatuan
    {
        get => _selSatuan;
        set { if (SetProperty(ref _selSatuan, value) && value != null) EditSatuan = new LookupItem { Id = value.Id, Nama = value.Nama, IsActive = value.IsActive }; }
    }

    public LookupItem EditSatuan { get => _editSatuan; set => SetProperty(ref _editSatuan, value); }

    private async Task LoadLookupsAsync()
    {
        KategoriList.Clear();
        foreach (var k in await AppServices.Mitra.ListLookupAsync("Kategori", false)) KategoriList.Add(k);
        SatuanList.Clear();
        foreach (var s in await AppServices.Mitra.ListLookupAsync("Satuan", false)) SatuanList.Add(s);
        EditKategori ??= new LookupItem();
        EditSatuan ??= new LookupItem();
    }

    private async Task SaveLookupAsync(string kind)
    {
        var item = kind == "Kategori" ? EditKategori : EditSatuan;
        if (item == null) return;
        await AppServices.Mitra.SaveLookupAsync(kind, item);
        ToastService.Success($"{kind} '{item.Nama}' tersimpan.");
        if (kind == "Kategori") EditKategori = new LookupItem(); else EditSatuan = new LookupItem();
        await LoadLookupsAsync();
    }

    private async Task ToggleLookupAsync(string kind)
    {
        var item = kind == "Kategori" ? SelectedKategori : SelectedSatuan;
        if (item == null) return;
        await AppServices.Mitra.SetLookupActiveAsync(kind, item, !item.IsActive);
        ToastService.Success($"{item.Nama} {(item.IsActive ? "dinonaktifkan" : "diaktifkan")}.");
        await LoadLookupsAsync();
    }

    // ================= Pengguna =================
    private User _selUser, _editUser;
    private string _newPassword = "";
    public ObservableCollection<User> Users { get; } = new();
    public ICommand NewUserCommand { get; }
    public ICommand SaveUserCommand { get; }
    public ICommand ResetPasswordCommand { get; }

    public List<KeyValuePair<string, string>> RoleOptions { get; } =
        Roles.All.Select(r => new KeyValuePair<string, string>(r, Roles.Label(r))).ToList();

    public User SelectedUser
    {
        get => _selUser;
        set
        {
            if (SetProperty(ref _selUser, value) && value != null)
                EditUser = new User { UserId = value.UserId, Username = value.Username, Nama = value.Nama, Role = value.Role, IsActive = value.IsActive };
        }
    }

    public User EditUser
    {
        get => _editUser;
        set
        {
            SetProperty(ref _editUser, value);
            OnPropertyChanged(nameof(IsNewUser));
            NewPassword = "";
        }
    }

    public bool IsNewUser => EditUser != null && EditUser.UserId == 0;
    public string NewPassword { get => _newPassword; set => SetProperty(ref _newPassword, value); }

    private async Task LoadUsersAsync()
    {
        var keep = SelectedUser?.UserId;
        Users.Clear();
        foreach (var u in await AppServices.Users.ListAsync()) Users.Add(u);
        SelectedUser = Users.FirstOrDefault(u => u.UserId == keep) ?? Users.FirstOrDefault();
    }

    private async Task SaveUserAsync()
    {
        await AppServices.Users.SaveAsync(EditUser, NewPassword);
        ToastService.Success($"Akun {EditUser.Username} tersimpan. Perubahan peran berlaku pada login berikutnya.");
        await LoadUsersAsync();
    }

    private async Task ResetPasswordAsync()
    {
        var pwd = DialogService.Prompt($"Masukkan kata sandi baru untuk {SelectedUser.Username} (minimal 6 karakter).",
            "Reset kata sandi", "Kata sandi baru", "", false, "Reset");
        if (pwd == null) return;
        await AppServices.Users.ResetPasswordAsync(SelectedUser.UserId, pwd);
        ToastService.Success($"Kata sandi {SelectedUser.Username} direset.");
    }

    // ================= Parameter =================
    private string _namaGerai, _alamatGerai, _teleponGerai, _ambang, _periodeKuota, _ukuranHalaman;
    private Barang _hetBarang;
    private string _hetBaru = "";
    public ICommand SaveParameterCommand { get; }
    public ICommand SaveHetCommand { get; }
    public ObservableCollection<Barang> BarangSubsidi { get; } = new();
    public List<string> PeriodeOptions { get; } = new() { "BULANAN", "TAHUNAN" };

    public string NamaGerai { get => _namaGerai; set => SetProperty(ref _namaGerai, value); }
    public string AlamatGerai { get => _alamatGerai; set => SetProperty(ref _alamatGerai, value); }
    public string TeleponGerai { get => _teleponGerai; set => SetProperty(ref _teleponGerai, value); }

    public string AmbangKedaluwarsa
    {
        get => _ambang;
        set { if (SetProperty(ref _ambang, value)) Validate(nameof(AmbangKedaluwarsa), int.TryParse(value, out var v) && v is >= 1 and <= 365, "Isi 1 sampai 365 hari"); }
    }

    public string PeriodeKuota { get => _periodeKuota; set => SetProperty(ref _periodeKuota, value); }

    public string UkuranHalaman
    {
        get => _ukuranHalaman;
        set { if (SetProperty(ref _ukuranHalaman, value)) Validate(nameof(UkuranHalaman), int.TryParse(value, out var v) && v is >= 10 and <= 200, "Isi 10 sampai 200"); }
    }

    public Barang HetBarang
    {
        get => _hetBarang;
        set { if (SetProperty(ref _hetBarang, value)) { HetBaru = Num.Format(value?.HET); OnPropertyChanged(nameof(HetInfo)); } }
    }

    public string HetBaru
    {
        get => _hetBaru;
        set { if (SetProperty(ref _hetBaru, value)) OnPropertyChanged(nameof(HetInfo)); }
    }

    public string HetInfo
    {
        get
        {
            if (HetBarang == null) return null;
            var info = $"Harga jual saat ini {Fmt.Rp(HetBarang.HargaUmum)}.";
            if (Num.TryParse(HetBaru, out var het) && het < HetBarang.HargaUmum)
                info += " HET baru lebih rendah dari harga jual: penyaluran barang ini akan diblokir sampai harganya disesuaikan (BR-07).";
            return info;
        }
    }

    private async Task LoadParameterAsync()
    {
        await AppServices.Settings.RefreshAsync();
        var s = AppServices.Settings;
        NamaGerai = s.NamaGerai;
        AlamatGerai = s.AlamatGerai;
        TeleponGerai = s.Get("TeleponGerai");
        AmbangKedaluwarsa = s.AmbangKedaluwarsaHari.ToString();
        PeriodeKuota = s.JenisPeriodeKuota;
        UkuranHalaman = s.UkuranHalaman.ToString();
        BarangSubsidi.Clear();
        foreach (var b in await AppServices.Barang.ListAsync("SUBSIDI")) BarangSubsidi.Add(b);
        HetBarang = BarangSubsidi.FirstOrDefault();
    }

    private async Task SaveParameterAsync()
    {
        if (HasErrors) { ToastService.Warning("Perbaiki isian yang ditandai merah."); return; }
        if (string.IsNullOrWhiteSpace(NamaGerai)) { ToastService.Warning("Nama gerai wajib diisi."); return; }
        await AppServices.Settings.SaveAsync(new Dictionary<string, string>
        {
            ["NamaGerai"] = NamaGerai.Trim(),
            ["AlamatGerai"] = AlamatGerai?.Trim() ?? "",
            ["TeleponGerai"] = TeleponGerai?.Trim() ?? "",
            ["AmbangKedaluwarsaHari"] = AmbangKedaluwarsa,
            ["PeriodeKuota"] = PeriodeKuota,
            ["UkuranHalaman"] = UkuranHalaman
        });
        ToastService.Success("Pengaturan tersimpan.");
    }

    private async Task SaveHetAsync()
    {
        if (!Num.TryParse(HetBaru, out var het)) { ToastService.Warning("Isi HET dengan angka."); return; }
        await AppServices.Barang.UpdateHetAsync(HetBarang, het);
        ToastService.Success($"HET {HetBarang.Nama} diperbarui menjadi {Fmt.Rp(het)}.");
        var id = HetBarang.BarangId;
        BarangSubsidi.Clear();
        foreach (var b in await AppServices.Barang.ListAsync("SUBSIDI")) BarangSubsidi.Add(b);
        HetBarang = BarangSubsidi.FirstOrDefault(b => b.BarangId == id);
    }

    // ================= Log aktivitas =================
    private DateTime _logDari = DateTime.Today.AddDays(-7), _logSampai = DateTime.Today;
    private User _logUser;
    private string _logAksi = "";
    public ICommand LoadLogCommand { get; }
    public ObservableCollection<Aktivitas> Log { get; } = new();
    public ObservableCollection<User> LogUsers { get; } = new();
    public ObservableCollection<string> LogAksiList { get; } = new();
    public DateTime LogDari { get => _logDari; set { if (SetProperty(ref _logDari, value)) ScheduleReload(LoadLogAsync); } }
    public DateTime LogSampai { get => _logSampai; set { if (SetProperty(ref _logSampai, value)) ScheduleReload(LoadLogAsync); } }
    public User LogUser { get => _logUser; set => SetProperty(ref _logUser, value); }
    public string LogAksi { get => _logAksi; set => SetProperty(ref _logAksi, value); }

    private async Task LoadLogAsync()
    {
        if (LogAksiList.Count == 0)
        {
            LogAksiList.Add("");
            foreach (var a in await AppServices.Activity.ListAksiAsync()) LogAksiList.Add(a);
            LogUsers.Add(new User { UserId = 0, Nama = "Semua pengguna" });
            if (IsManajer) foreach (var u in await AppServices.Users.ListAsync()) LogUsers.Add(u);
            LogUser = LogUsers[0];
        }
        Log.Clear();
        foreach (var a in await AppServices.Activity.ListAsync(LogDari, LogSampai,
                     LogUser == null || LogUser.UserId == 0 ? null : LogUser.UserId,
                     string.IsNullOrEmpty(LogAksi) ? null : LogAksi))
            Log.Add(a);
    }

    // ================= Backup =================
    public ICommand BackupCommand { get; }
    public ICommand RestoreCommand { get; }
    public string BackupInfo =>
        "File backup ditulis oleh layanan SQL Server, jadi simpan di folder yang dapat diakses layanan itu (mis. C:\\StokaraBackup). " +
        "Salin hasilnya ke flashdisk atau media lain secara berkala.";

    private async Task BackupAsync()
    {
        var folder = @"C:\StokaraBackup";
        try { Directory.CreateDirectory(folder); } catch { /* pengguna memilih lokasi lain */ }
        var path = DialogService.SaveFile("Backup SQL Server (*.bak)|*.bak", $"STOKARA_{DateTime.Now:yyyyMMdd_HHmm}.bak");
        if (path == null) return;
        IsBusy = true;
        try
        {
            await AppServices.Backup.BackupAsync(path);
            ToastService.Success($"Backup tersimpan: {path}");
        }
        finally { IsBusy = false; }
    }

    private async Task RestoreAsync()
    {
        var path = DialogService.OpenFile("Backup SQL Server (*.bak)|*.bak");
        if (path == null) return;
        if (!DialogService.Confirm("Memulihkan database akan MENGGANTI seluruh data saat ini dengan isi file backup. " +
                                   "Pastikan tidak ada komputer lain yang sedang memakai STOKARA. Lanjutkan?",
                "Pulihkan database", "Pulihkan", true)) return;
        IsBusy = true;
        try
        {
            await AppServices.Backup.RestoreAsync(path);
            DialogService.Confirm("Database berhasil dipulihkan. Aplikasi akan kembali ke halaman login.", "Pemulihan selesai", "OK");
            RestoreCompleted?.Invoke();
        }
        finally { IsBusy = false; }
    }

    /// <summary>Diteruskan ke jendela utama untuk logout setelah restore.</summary>
    public static event Action RestoreCompleted;

    // ================= Umum =================
    public override async Task LoadAsync() => await LoadTabAsync();

    private async Task SafeLoadTab()
    {
        try { await LoadTabAsync(); } catch (Exception ex) { ErrorHandler.Handle(ex); }
    }

    private Task LoadTabAsync() => Tab switch
    {
        1 when IsManajer => LoadLookupsAsync(),
        2 when IsManajer => LoadUsersAsync(),
        3 when IsManajer => LoadParameterAsync(),
        4 when CanSeeLog => LoadLogAsync(),
        _ => Task.CompletedTask
    };
}
