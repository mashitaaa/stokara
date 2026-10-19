using System.Collections.ObjectModel;
using System.Windows.Input;
using Stokara.Helpers;
using Stokara.Infrastructure;
using Stokara.Models;

namespace Stokara.ViewModels;

/// <summary>Konsinyasi: stok titipan, hutang penitip, pelunasan FIFO (FR-24-26, A-05, A-07).</summary>
public sealed class KonsinyasiViewModel : ViewModelBase, IShortcutTarget
{
    private PenitipRingkas _selected;
    private bool _hanyaBelum = true;
    private string _bayarText = "";
    private string _catatan = "";

    public KonsinyasiViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        BayarCommand = new AsyncRelayCommand(BayarAsync, () => Selected != null && Selected.Sisa > 0 && CanBayar);
        LunasiSemuaCommand = new RelayCommand(() => BayarText = Num.Format(Selected?.Sisa ?? 0), () => Selected != null && CanBayar);
    }

    public ICommand RefreshCommand { get; }
    public ICommand BayarCommand { get; }
    public ICommand LunasiSemuaCommand { get; }

    public bool CanBayar => Session.Is(Roles.Manajer, Roles.Pengurus);

    public ObservableCollection<PenitipRingkas> Penitip { get; } = new();
    public ObservableCollection<HutangRow> Hutang { get; } = new();
    public ObservableCollection<PelunasanRow> Pelunasan { get; } = new();
    public ObservableCollection<Barang> StokTitipan { get; } = new();

    public decimal TotalSisa => Penitip.Sum(p => p.Sisa);
    public decimal TotalKomisi => Penitip.Sum(p => p.TotalKomisi);
    public decimal TotalNilaiTitipan => Penitip.Sum(p => p.StokTitipanNilai);

    public PenitipRingkas Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value)) _ = LoadDetailSafe();
        }
    }

    public bool HanyaBelumLunas
    {
        get => _hanyaBelum;
        set { if (SetProperty(ref _hanyaBelum, value)) _ = LoadDetailSafe(); }
    }

    public string BayarText
    {
        get => _bayarText;
        set
        {
            if (!SetProperty(ref _bayarText, value)) return;
            var ok = value == "" || (Num.TryParse(value, out var v) && v > 0 && v <= (Selected?.Sisa ?? 0));
            Validate(nameof(BayarText), ok, $"Isi 1 sampai {Fmt.Rp(Selected?.Sisa ?? 0)} (BR-13)");
        }
    }

    public string Catatan { get => _catatan; set => SetProperty(ref _catatan, value); }

    public override async Task LoadAsync()
    {
        var keep = Selected?.PenitipId;
        Penitip.Clear();
        foreach (var p in await AppServices.Konsinyasi.RingkasanAsync()) Penitip.Add(p);
        StokTitipan.Clear();
        foreach (var b in await AppServices.Barang.ListAsync("TITIPAN")) StokTitipan.Add(b);
        OnPropertyChanged(nameof(TotalSisa));
        OnPropertyChanged(nameof(TotalKomisi));
        OnPropertyChanged(nameof(TotalNilaiTitipan));
        Selected = Penitip.FirstOrDefault(p => p.PenitipId == keep) ?? Penitip.FirstOrDefault();
    }

    private async Task LoadDetailSafe()
    {
        try { await LoadDetailAsync(); } catch (Exception ex) { ErrorHandler.Handle(ex); }
    }

    private async Task LoadDetailAsync()
    {
        Hutang.Clear();
        Pelunasan.Clear();
        if (Selected == null) return;
        foreach (var h in await AppServices.Konsinyasi.ListHutangAsync(Selected.PenitipId, HanyaBelumLunas)) Hutang.Add(h);
        foreach (var p in await AppServices.Konsinyasi.ListPelunasanAsync(Selected.PenitipId)) Pelunasan.Add(p);
    }

    private async Task BayarAsync()
    {
        if (!Num.TryParse(BayarText, out var jumlah) || jumlah <= 0)
        {
            ToastService.Warning("Isi jumlah pembayaran.");
            return;
        }
        if (!DialogService.Confirm($"Catat pembayaran {Fmt.Rp(jumlah)} kepada {Selected.Nama}? Pembayaran dialokasikan ke hutang tertua lebih dulu.",
                "Pelunasan penitip", "Catat pembayaran")) return;
        var nomor = await AppServices.Konsinyasi.BayarAsync(Selected.PenitipId, jumlah, Catatan);
        ToastService.Success($"Pelunasan {nomor} tercatat.");
        BayarText = Catatan = "";
        await LoadAsync();
    }

    public void OnNew() { }
    public void OnSave() => BayarCommand.Execute(null);
    public void OnEscape() => BayarText = "";
}
