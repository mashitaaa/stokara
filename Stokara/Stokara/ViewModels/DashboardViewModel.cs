using System.Collections.ObjectModel;
using System.Windows.Input;
using Stokara.Helpers;
using Stokara.Infrastructure;
using Stokara.Models;

namespace Stokara.ViewModels;

/// <summary>Dashboard (FR-04-07): kartu statistik, grafik, panel peringatan, aktivitas terbaru.</summary>
public sealed class DashboardViewModel : ViewModelBase
{
    private DashboardStats _stats = new();
    private int _hari = 7;

    public DashboardViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        SetHariCommand = new AsyncRelayCommand(async p =>
        {
            Hari = int.Parse(p.ToString());
            await LoadAsync();
        });
    }

    public ICommand RefreshCommand { get; }
    public ICommand SetHariCommand { get; }

    public DashboardStats Stats
    {
        get => _stats;
        private set
        {
            SetProperty(ref _stats, value);
            OnPropertyChanged(nameof(SubsidiPersen));
            OnPropertyChanged(nameof(SubsidiSubtitle));
            OnPropertyChanged(nameof(PenjualanSubtitle));
        }
    }

    public int Hari
    {
        get => _hari;
        set
        {
            if (SetProperty(ref _hari, value)) OnPropertyChanged(nameof(GrafikJudul));
        }
    }

    public string Sapaan
    {
        get
        {
            var h = DateTime.Now.Hour;
            var waktu = h < 11 ? "Selamat pagi" : h < 15 ? "Selamat siang" : h < 18 ? "Selamat sore" : "Selamat malam";
            return $"{waktu}, {Session.Current?.Nama}";
        }
    }

    public string Tanggal => DateTime.Now.ToString("dddd, d MMMM yyyy", Fmt.Id);
    public string GrafikJudul => Session.Is(Roles.Kasir) ? $"Penjualan Anda {Hari} hari terakhir" : $"Penjualan {Hari} hari terakhir";
    public string PenjualanSubtitle => $"{Stats.TransaksiHariIni} transaksi" + (Session.Is(Roles.Kasir) ? " oleh Anda" : "");
    public double SubsidiPersen => Stats.SubsidiKuota <= 0 ? 0 : (double)(Stats.SubsidiTersalur / Stats.SubsidiKuota * 100);
    public string SubsidiSubtitle => $"dari {Fmt.Num(Stats.SubsidiKuota)} unit kuota ({SubsidiPersen:0}%)";
    public string PeriodeKuota => AppServices.Settings.PeriodeSekarang;

    public bool ShowFinance => !Session.Is(Roles.Kasir, Roles.Gudang);
    public bool ShowSales => !Session.Is(Roles.Gudang);
    public bool ShowExpiry => !Session.Is(Roles.Kasir);
    public bool ShowAktivitas => !Session.Is(Roles.Kasir);

    public ObservableCollection<ChartPoint> Penjualan { get; } = new();
    public ObservableCollection<ChartPoint> Terlaris { get; } = new();
    public ObservableCollection<ChartPoint> Kuota { get; } = new();
    public ObservableCollection<PeringatanRow> StokKritis { get; } = new();
    public ObservableCollection<PeringatanRow> Kedaluwarsa { get; } = new();
    public ObservableCollection<Aktivitas> Aktivitas { get; } = new();

    public override async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var d = await AppServices.Dashboard.LoadAsync(Hari);
            Stats = d.Stats;
            Replace(Penjualan, d.Penjualan);
            Replace(Terlaris, d.Terlaris);
            Replace(Kuota, d.Kuota);
            Replace(StokKritis, d.StokKritis);
            Replace(Kedaluwarsa, d.Kedaluwarsa);
            Replace(Aktivitas, d.Aktivitas);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var i in items) target.Add(i);
    }
}
