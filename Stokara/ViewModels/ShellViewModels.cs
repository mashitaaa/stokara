using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using Stokara.Helpers;
using Stokara.Infrastructure;
using Stokara.Models;

namespace Stokara.ViewModels;

/// <summary>Halaman yang menanggapi pintasan keyboard global (Ctrl+N, Ctrl+S, Esc) - PRD 10.1.</summary>
public interface IShortcutTarget
{
    void OnNew();
    void OnSave();
    void OnEscape();
}

public sealed class LoginViewModel : ViewModelBase
{
    private string _username = "";
    private string _error;

    public string Username
    {
        get => _username;
        set { SetProperty(ref _username, value); Error = null; }
    }

    public string Error
    {
        get => _error;
        set => SetProperty(ref _error, value);
    }

    public string NamaGerai => AppServices.Settings?.NamaGerai ?? "Gerai Kopdes Merah Putih";

    /// <summary>Dipanggil dari View dengan password dari PasswordBox (password tidak disimpan sebagai properti).</summary>
    public async Task<bool> LoginAsync(string password)
    {
        if (IsBusy) return false;
        IsBusy = true;
        Error = null;
        try
        {
            await AppServices.Auth.LoginAsync(Username, password);
            return true;
        }
        catch (BusinessException ex)
        {
            Error = ex.Message;
        }
        catch (Microsoft.Data.SqlClient.SqlException ex)
        {
            Logger.Error("Login", ex);
            Error = ErrorHandler.Friendly(ex);
        }
        finally
        {
            IsBusy = false;
        }
        return false;
    }
}

public sealed class NavItem : ObservableObject
{
    private bool _isActive;
    public string Key { get; init; }
    public string Title { get; init; }
    public string Icon { get; init; }
    public string Group { get; init; }
    public string Shortcut { get; init; }
    public string[] Roles { get; init; }
    public Func<ViewModelBase> Create { get; init; }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
}

public sealed class NavGroup
{
    public string Title { get; init; }
    public List<NavItem> Items { get; init; }
}

/// <summary>Shell aplikasi: sidebar, header, navigasi, tema, status database, dan logout.</summary>
public sealed class MainViewModel : ViewModelBase
{
    private ViewModelBase _currentPage;
    private NavItem _current;
    private bool _dbOnline = true;
    private readonly DispatcherTimer _pingTimer;

    public MainViewModel()
    {
        var all = new[] { Roles.Manajer, Roles.Kasir, Roles.Gudang, Roles.Pengurus };
        var items = new List<NavItem>
        {
            new() { Key = "dashboard", Title = "Dashboard", Icon = "", Group = "HARIAN", Roles = all, Create = () => new DashboardViewModel() },
            new() { Key = "kasir", Title = "Kasir", Icon = "", Group = "HARIAN", Shortcut = "F2", Roles = new[] { Roles.Manajer, Roles.Kasir }, Create = () => new KasirViewModel() },
            new() { Key = "riwayat", Title = "Riwayat Transaksi", Icon = "", Group = "HARIAN", Roles = new[] { Roles.Manajer, Roles.Kasir, Roles.Pengurus }, Create = () => new RiwayatViewModel() },
            new() { Key = "barang", Title = "Barang", Icon = "", Group = "STOK", Roles = all, Create = () => new BarangViewModel() },
            new() { Key = "stok", Title = "Stok Masuk & Keluar", Icon = "", Group = "STOK", Roles = new[] { Roles.Manajer, Roles.Gudang, Roles.Pengurus }, Create = () => new StokViewModel() },
            new() { Key = "opname", Title = "Stok Opname", Icon = "", Group = "STOK", Roles = new[] { Roles.Manajer, Roles.Gudang, Roles.Pengurus }, Create = () => new OpnameViewModel() },
            new() { Key = "mitra", Title = "Mitra", Icon = "", Group = "MITRA", Roles = all, Create = () => new MitraViewModel() },
            new() { Key = "konsinyasi", Title = "Konsinyasi", Icon = "", Group = "MITRA", Roles = new[] { Roles.Manajer, Roles.Gudang, Roles.Pengurus }, Create = () => new KonsinyasiViewModel() },
            new() { Key = "laporan", Title = "Laporan", Icon = "", Group = "LAPORAN & SISTEM", Roles = all, Create = () => new LaporanViewModel() },
            new() { Key = "pengaturan", Title = "Pengaturan", Icon = "", Group = "LAPORAN & SISTEM", Roles = all, Create = () => new PengaturanViewModel() },
            new() { Key = "bantuan", Title = "Bantuan", Icon = "", Group = "LAPORAN & SISTEM", Roles = all, Create = () => new BantuanViewModel() }
        };
        // Menu yang tidak diizinkan tidak ditampilkan (aksi tetap diblokir di service).
        var allowed = items.Where(i => Session.Is(i.Roles)).ToList();
        Groups = new ObservableCollection<NavGroup>(allowed.GroupBy(i => i.Group)
            .Select(g => new NavGroup { Title = g.Key, Items = g.ToList() }));
        _items = allowed;

        NavigateCommand = new RelayCommand(p => Navigate(p as string));
        ToggleThemeCommand = new AsyncRelayCommand(ToggleThemeAsync);
        RefreshCommand = new AsyncRelayCommand(() => CurrentPage?.LoadAsync() ?? Task.CompletedTask);

        DbStatus.Changed += OnDbStatusChanged;
        _pingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _pingTimer.Tick += async (_, _) => DbStatus.Set(await AppServices.Db.PingAsync());
        _pingTimer.Start();

        Navigate("dashboard");
    }

    private readonly List<NavItem> _items;

    public ObservableCollection<NavGroup> Groups { get; }
    public ICommand NavigateCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public ICommand RefreshCommand { get; }

    public string UserNama => Session.Current?.Nama;
    public string UserRole => Session.Current?.RoleLabel;
    public string UserInisial => Session.Current?.Inisial;
    public string NamaGerai => AppServices.Settings.NamaGerai;
    public bool IsDark => ThemeService.Current == "Dark";
    public string ThemeIcon => IsDark ? "" : "";
    public string ThemeTooltip => IsDark ? "Ganti ke tema terang" : "Ganti ke tema gelap";

    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public string PageTitle => _current?.Title;

    public bool DbOnline
    {
        get => _dbOnline;
        set
        {
            if (SetProperty(ref _dbOnline, value)) OnPropertyChanged(nameof(DbStatusText));
        }
    }

    public string DbStatusText => DbOnline ? "Database terhubung" : "Database terputus";

    public void Navigate(string key)
    {
        var item = _items.FirstOrDefault(i => i.Key == key);
        if (item == null)
        {
            if (key != null) ToastService.Warning("Menu ini tidak tersedia untuk peran Anda.");
            return;
        }
        foreach (var i in _items) i.IsActive = i == item;
        _current = item;
        var page = item.Create();
        CurrentPage = page;
        OnPropertyChanged(nameof(PageTitle));
        _ = LoadPageAsync(page);
    }

    private static async Task LoadPageAsync(ViewModelBase page)
    {
        try
        {
            page.IsBusy = true;
            await page.LoadAsync();
            DbStatus.Set(true);
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex);
        }
        finally
        {
            page.IsBusy = false;
        }
    }

    private async Task ToggleThemeAsync()
    {
        var next = IsDark ? "Light" : "Dark";
        ThemeService.Apply(next);
        OnPropertyChanged(nameof(IsDark));
        OnPropertyChanged(nameof(ThemeIcon));
        OnPropertyChanged(nameof(ThemeTooltip));
        await AppServices.Auth.SetThemeAsync(next);   // disimpan per pengguna (A-19)
    }

    private void OnDbStatusChanged(bool online) => DbOnline = online;

    public async Task LogoutAsync()
    {
        Stop();
        await AppServices.Auth.LogoutAsync();
    }

    public void Stop()
    {
        _pingTimer.Stop();
        DbStatus.Changed -= OnDbStatusChanged;
    }

    // ---- Pintasan keyboard ----
    public void ShortcutNew() => (CurrentPage as IShortcutTarget)?.OnNew();
    public void ShortcutSave() => (CurrentPage as IShortcutTarget)?.OnSave();
    public void ShortcutEscape() => (CurrentPage as IShortcutTarget)?.OnEscape();
}
