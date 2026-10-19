using System.Collections.ObjectModel;
using System.Windows.Input;
using Stokara.Helpers;
using Stokara.Infrastructure;
using Stokara.Models;

namespace Stokara.ViewModels;

/// <summary>Riwayat transaksi, detail, cetak ulang, void (FR-21, FR-22), dan tutup kasir (FR-23).</summary>
public sealed class RiwayatViewModel : ViewModelBase, IShortcutTarget
{
    private DateTime _dari = DateTime.Today.AddDays(-6);
    private DateTime _sampai = DateTime.Today;
    private string _jenis = "";
    private string _keyword = "";
    private TransaksiRow _selected;
    private bool _todayClosed;
    private int _jumlahHariIni;
    private decimal _totalHariIni;
    private string _modalText = "";
    private string _fisikText = "";
    private string _catatan = "";

    public RiwayatViewModel()
    {
        SearchCommand = new AsyncRelayCommand(LoadTransaksiAsync);
        PrintCommand = new AsyncRelayCommand(PrintAsync, () => Selected != null);
        VoidCommand = new AsyncRelayCommand(VoidAsync, () => Selected?.Status == "AKTIF" && CanVoid);
        TutupCommand = new AsyncRelayCommand(TutupAsync, () => !TodayClosed && CanTutup);
    }

    public ICommand SearchCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand VoidCommand { get; }
    public ICommand TutupCommand { get; }

    public bool IsKasir => Session.Is(Roles.Kasir);
    public bool CanFilterDate => !IsKasir;
    public bool CanVoid => Session.Is(Roles.Manajer);
    public bool CanTutup => Session.Is(Roles.Manajer, Roles.Kasir);
    public string Info => IsKasir ? "Menampilkan transaksi Anda hari ini." : "Transaksi penjualan dan penyaluran subsidi.";

    public List<KeyValuePair<string, string>> JenisOptions { get; } = new()
    {
        new("", "Semua jenis"), new("PENJUALAN", "Penjualan"), new("PENYALURAN_SUBSIDI", "Penyaluran Subsidi")
    };

    public DateTime Dari { get => _dari; set { if (SetProperty(ref _dari, value)) ScheduleReload(LoadTransaksiAsync); } }
    public DateTime Sampai { get => _sampai; set { if (SetProperty(ref _sampai, value)) ScheduleReload(LoadTransaksiAsync); } }
    public string Jenis { get => _jenis; set { if (SetProperty(ref _jenis, value)) _ = SafeLoad(); } }
    public string Keyword { get => _keyword; set => SetProperty(ref _keyword, value); }

    public ObservableCollection<TransaksiRow> Items { get; } = new();
    public ObservableCollection<TransaksiDetailRow> Detail { get; } = new();
    public ObservableCollection<TutupHariRow> RiwayatTutup { get; } = new();

    public TransaksiRow Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value)) _ = LoadDetailAsync();
        }
    }

    public decimal TotalAktif => Items.Where(i => i.Status == "AKTIF").Sum(i => i.Total);
    public int JumlahAktif => Items.Count(i => i.Status == "AKTIF");

    // ---- Tutup kasir ----
    public bool TodayClosed { get => _todayClosed; private set => SetProperty(ref _todayClosed, value); }
    public int JumlahHariIni { get => _jumlahHariIni; private set => SetProperty(ref _jumlahHariIni, value); }
    public decimal TotalHariIni { get => _totalHariIni; private set { SetProperty(ref _totalHariIni, value); RaiseKas(); } }

    public string ModalText
    {
        get => _modalText;
        set
        {
            if (SetProperty(ref _modalText, value))
            {
                Validate(nameof(ModalText), value == "" || Num.TryParse(value, out _), "Masukkan angka");
                RaiseKas();
            }
        }
    }

    public string FisikText
    {
        get => _fisikText;
        set
        {
            if (SetProperty(ref _fisikText, value))
            {
                Validate(nameof(FisikText), value == "" || Num.TryParse(value, out _), "Masukkan angka");
                RaiseKas();
            }
        }
    }

    public string Catatan { get => _catatan; set => SetProperty(ref _catatan, value); }

    public decimal UangSeharusnya => (Num.TryParse(ModalText, out var m) ? m : 0) + TotalHariIni;
    public decimal Selisih => (Num.TryParse(FisikText, out var f) ? f : 0) - UangSeharusnya;
    public string SelisihLabel => string.IsNullOrWhiteSpace(FisikText) ? "-"
        : Selisih == 0 ? "Sesuai" : (Selisih > 0 ? "Lebih " : "Kurang ") + Fmt.Rp(Math.Abs(Selisih));

    private void RaiseKas()
    {
        OnPropertyChanged(nameof(UangSeharusnya));
        OnPropertyChanged(nameof(Selisih));
        OnPropertyChanged(nameof(SelisihLabel));
    }

    public override async Task LoadAsync()
    {
        await LoadTransaksiAsync();
        await LoadTutupAsync();
    }

    private async Task SafeLoad()
    {
        try { await LoadTransaksiAsync(); } catch (Exception ex) { ErrorHandler.Handle(ex); }
    }

    private async Task LoadTransaksiAsync()
    {
        if (Sampai < Dari) { ToastService.Warning("Tanggal akhir tidak boleh sebelum tanggal awal."); return; }
        var list = await AppServices.Penjualan.ListAsync(Dari, Sampai, string.IsNullOrEmpty(Jenis) ? null : Jenis, Keyword);
        Items.Clear();
        foreach (var i in list) Items.Add(i);
        OnPropertyChanged(nameof(TotalAktif));
        OnPropertyChanged(nameof(JumlahAktif));
        Selected = Items.FirstOrDefault();
    }

    private async Task LoadDetailAsync()
    {
        Detail.Clear();
        if (Selected == null) return;
        try
        {
            foreach (var d in await AppServices.Penjualan.DetailAsync(Selected.Id)) Detail.Add(d);
        }
        catch (Exception ex) { ErrorHandler.Handle(ex); }
    }

    private async Task LoadTutupAsync()
    {
        TodayClosed = await AppServices.Penjualan.IsTodayClosedAsync();
        var (jumlah, total) = await AppServices.Penjualan.RingkasanHariIniAsync();
        JumlahHariIni = jumlah;
        TotalHariIni = total;
        RiwayatTutup.Clear();
        foreach (var r in await AppServices.Penjualan.ListTutupHariAsync()) RiwayatTutup.Add(r);
    }

    private async Task PrintAsync()
    {
        var r = await AppServices.Penjualan.ReceiptAsync(Selected.Id);
        if (r != null) DialogService.ShowReceipt(r);
    }

    private async Task VoidAsync()
    {
        var row = Selected;
        var alasan = DialogService.Prompt(
            $"Transaksi {row.NomorDokumen} ({Fmt.Rp(row.Total)}) akan dibatalkan. Stok, kuota subsidi, dan hutang penitip akan dipulihkan. " +
            "Riwayat tetap tersimpan.", "Batalkan transaksi", "Alasan pembatalan", "", true, "Batalkan transaksi");
        if (alasan == null) return;
        await AppServices.Penjualan.VoidAsync(row.Id, alasan);
        ToastService.Success($"{row.NomorDokumen} dibatalkan.");
        await LoadAsync();
    }

    private async Task TutupAsync()
    {
        if (!Num.TryParse(ModalText == "" ? "0" : ModalText, out var modal) || !Num.TryParse(FisikText, out var fisik))
        {
            ToastService.Warning("Isi modal awal dan uang fisik di laci dengan angka.");
            return;
        }
        var msg = $"Tutup kasir hari ini?\n\nTransaksi: {JumlahHariIni}\nTotal penjualan: {Fmt.Rp(TotalHariIni)}\n" +
                  $"Uang seharusnya: {Fmt.Rp(modal + TotalHariIni)}\nUang fisik: {Fmt.Rp(fisik)}\nSelisih: {SelisihLabel}\n\n" +
                  "Setelah ditutup, transaksi hari ini tidak dapat ditambah atau dibatalkan.";
        if (!DialogService.Confirm(msg, "Tutup kasir", "Tutup hari")) return;
        await AppServices.Penjualan.TutupHariAsync(modal, fisik, Catatan);
        ToastService.Success("Kasir hari ini sudah ditutup.");
        ModalText = FisikText = Catatan = "";
        await LoadTutupAsync();
    }

    public void OnNew() { }
    public void OnSave() => SearchCommand.Execute(null);
    public void OnEscape() => Keyword = "";
}
