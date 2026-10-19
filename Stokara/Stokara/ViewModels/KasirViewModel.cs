using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using Stokara.Helpers;
using Stokara.Infrastructure;
using Stokara.Models;
using Stokara.Services;

namespace Stokara.ViewModels;

/// <summary>
/// Layar kasir dua mode (FR-17, FR-18): Penjualan (umum/anggota) dan Penyaluran Subsidi.
/// Layar hanya membantu (cek awal stok, sisa kuota); keputusan akhir ada di PenjualanService.
/// </summary>
public sealed class KasirViewModel : ViewModelBase, IShortcutTarget
{
    private string _mode = "PENJUALAN";
    private string _searchText = "";
    private bool _isSuggestOpen;
    private Barang _selectedSuggestion;
    private bool _isAnggota;
    private string _anggotaSearch = "";
    private Anggota _selectedAnggota;
    private string _penerimaSearch = "";
    private PenerimaSubsidi _selectedPenerima;
    private string _uangText = "";
    private LineItem _selectedItem;
    private readonly DispatcherTimer _searchTimer;
    private readonly DispatcherTimer _anggotaTimer;
    private readonly DispatcherTimer _penerimaTimer;
    private string _lastNomor;

    public KasirViewModel()
    {
        _searchTimer = Debounce(async () => await SearchBarangAsync());
        _anggotaTimer = Debounce(async () => await SearchAnggotaAsync());
        _penerimaTimer = Debounce(async () => await SearchPenerimaAsync());

        Cart.CollectionChanged += OnCartChanged;
        ScanCommand = new AsyncRelayCommand(ScanAsync);
        AddSuggestionCommand = new RelayCommand(p => { if (p is Barang b) AddBarang(b); });
        IncreaseCommand = new RelayCommand(p => { if (p is LineItem i) ChangeQty(i, 1); });
        DecreaseCommand = new RelayCommand(p => { if (p is LineItem i) ChangeQty(i, -1); });
        RemoveCommand = new RelayCommand(p => { if (p is LineItem i) Cart.Remove(i); });
        ClearCommand = new RelayCommand(() => ClearCart(true), () => Cart.Count > 0);
        BayarCommand = new AsyncRelayCommand(BayarAsync, () => Cart.Count > 0 && !IsBusy);
        UangPasCommand = new RelayCommand(() => UangText = Total.ToString("0", Fmt.Id));
        UangCepatCommand = new RelayCommand(p => UangText = p?.ToString());
        SetModeCommand = new RelayCommand(p => SetMode(p as string));
        PilihPenerimaCommand = new AsyncRelayCommand(async p => { if (p is PenerimaSubsidi ps) await SelectPenerimaAsync(ps); });
        PilihAnggotaCommand = new RelayCommand(p => { if (p is Anggota a) SelectAnggota(a); });
        ResetPelangganCommand = new RelayCommand(() => { SelectedAnggota = null; SelectedPenerima = null; });
        CetakUlangCommand = new AsyncRelayCommand(CetakUlangAsync, () => _lastNomor != null);
    }

    private static DispatcherTimer Debounce(Func<Task> action)
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        t.Tick += async (_, _) =>
        {
            t.Stop();
            try { await action(); } catch (Exception ex) { ErrorHandler.Handle(ex); }
        };
        return t;
    }

    // ============================ Properti ============================

    public ObservableCollection<LineItem> Cart { get; } = new();
    public ObservableCollection<Barang> Suggestions { get; } = new();
    public ObservableCollection<Anggota> AnggotaResults { get; } = new();
    public ObservableCollection<PenerimaSubsidi> PenerimaResults { get; } = new();
    public ObservableCollection<SisaKuota> SisaKuota { get; } = new();

    public ICommand ScanCommand { get; }
    public ICommand AddSuggestionCommand { get; }
    public ICommand IncreaseCommand { get; }
    public ICommand DecreaseCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand BayarCommand { get; }
    public ICommand UangPasCommand { get; }
    public ICommand UangCepatCommand { get; }
    public ICommand SetModeCommand { get; }
    public ICommand PilihPenerimaCommand { get; }
    public ICommand PilihAnggotaCommand { get; }
    public ICommand ResetPelangganCommand { get; }
    public ICommand CetakUlangCommand { get; }

    public string Mode
    {
        get => _mode;
        private set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsSubsidi));
                OnPropertyChanged(nameof(IsPenjualan));
                OnPropertyChanged(nameof(SearchPlaceholder));
                RaiseModeChecks();
            }
        }
    }

    /// <summary>Untuk binding dua arah tombol segmen mode.</summary>
    public bool ModePenjualanChecked
    {
        get => IsPenjualan;
        set { if (value) SetMode("PENJUALAN"); }
    }

    public bool ModeSubsidiChecked
    {
        get => IsSubsidi;
        set { if (value) SetMode("PENYALURAN_SUBSIDI"); }
    }

    private void RaiseModeChecks()
    {
        OnPropertyChanged(nameof(ModePenjualanChecked));
        OnPropertyChanged(nameof(ModeSubsidiChecked));
    }

    public bool IsSubsidi => Mode == "PENYALURAN_SUBSIDI";
    public bool IsPenjualan => !IsSubsidi;
    public string SearchPlaceholder => IsSubsidi
        ? "Scan barcode atau ketik nama barang subsidi… (Enter)"
        : "Scan barcode atau ketik nama/SKU barang… (Enter)";
    public string Periode => AppServices.Settings.PeriodeSekarang;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value)) return;
            _searchTimer.Stop();
            if (string.IsNullOrWhiteSpace(value)) { IsSuggestOpen = false; Suggestions.Clear(); }
            else _searchTimer.Start();
        }
    }

    public bool IsSuggestOpen
    {
        get => _isSuggestOpen;
        set => SetProperty(ref _isSuggestOpen, value);
    }

    public Barang SelectedSuggestion
    {
        get => _selectedSuggestion;
        set => SetProperty(ref _selectedSuggestion, value);
    }

    public LineItem SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    // ---- Pelanggan (mode Penjualan) ----
    public bool IsAnggota
    {
        get => _isAnggota;
        set
        {
            if (!SetProperty(ref _isAnggota, value)) return;
            if (!value) SelectedAnggota = null;
            OnPropertyChanged(nameof(IsUmum));
        }
    }

    public bool IsUmum
    {
        get => !IsAnggota;
        set => IsAnggota = !value;
    }

    public string AnggotaSearch
    {
        get => _anggotaSearch;
        set
        {
            if (!SetProperty(ref _anggotaSearch, value)) return;
            _anggotaTimer.Stop();
            if (string.IsNullOrWhiteSpace(value)) AnggotaResults.Clear(); else _anggotaTimer.Start();
        }
    }

    public Anggota SelectedAnggota
    {
        get => _selectedAnggota;
        set
        {
            if (!SetProperty(ref _selectedAnggota, value)) return;
            OnPropertyChanged(nameof(PelangganLabel));
            Reprice();
        }
    }

    public string PelangganLabel => SelectedAnggota != null ? $"{SelectedAnggota.Nama} · {SelectedAnggota.NomorAnggota}" : "Pelanggan umum";

    // ---- Penerima (mode Subsidi) ----
    public string PenerimaSearch
    {
        get => _penerimaSearch;
        set
        {
            if (!SetProperty(ref _penerimaSearch, value)) return;
            _penerimaTimer.Stop();
            if (string.IsNullOrWhiteSpace(value)) PenerimaResults.Clear(); else _penerimaTimer.Start();
        }
    }

    public PenerimaSubsidi SelectedPenerima
    {
        get => _selectedPenerima;
        set
        {
            if (!SetProperty(ref _selectedPenerima, value)) return;
            OnPropertyChanged(nameof(HasPenerima));
            OnPropertyChanged(nameof(PenerimaAktif));
            if (value == null) SisaKuota.Clear();
        }
    }

    public bool HasPenerima => SelectedPenerima != null;
    public bool PenerimaAktif => SelectedPenerima?.Status == "AKTIF";

    // ---- Pembayaran ----
    public decimal Total => Cart.Sum(i => i.Subtotal);
    public int JumlahItem => Cart.Count;

    public string UangText
    {
        get => _uangText;
        set
        {
            if (!SetProperty(ref _uangText, value)) return;
            OnPropertyChanged(nameof(Kembalian));
            OnPropertyChanged(nameof(UangKurang));
            Validate(nameof(UangText), value == "" || Num.TryParse(value, out _), "Masukkan angka, mis. 50000");
        }
    }

    public decimal UangDiterima => Num.TryParse(UangText, out var v) ? v : 0;
    public decimal Kembalian => Math.Max(0, UangDiterima - Total);
    public bool UangKurang => Cart.Count > 0 && UangDiterima < Total;

    // ============================ Logika layar ============================

    private void OnCartChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null) foreach (INotifyPropertyChanged i in e.NewItems) i.PropertyChanged += OnItemChanged;
        if (e.OldItems != null) foreach (INotifyPropertyChanged i in e.OldItems) i.PropertyChanged -= OnItemChanged;
        RaiseTotals();
    }

    private void OnItemChanged(object sender, PropertyChangedEventArgs e) => RaiseTotals();

    private void RaiseTotals()
    {
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(JumlahItem));
        OnPropertyChanged(nameof(Kembalian));
        OnPropertyChanged(nameof(UangKurang));
    }

    private void SetMode(string mode)
    {
        if (mode == null || mode == Mode) return;
        if (Cart.Count > 0 && !DialogService.Confirm("Keranjang akan dikosongkan karena mode berganti. Lanjutkan?", "Ganti mode"))
        {
            // Kembalikan pilihan segmen setelah binding selesai memproses klik.
            System.Windows.Application.Current.Dispatcher.InvokeAsync(RaiseModeChecks);
            return;
        }
        ClearCart(false);
        SelectedAnggota = null;
        IsAnggota = false;
        SelectedPenerima = null;
        Mode = mode;
    }

    private string JenisFilter => IsSubsidi ? "SUBSIDI" : "NONSUBSIDI";

    private async Task SearchBarangAsync()
    {
        var kw = SearchText;
        if (string.IsNullOrWhiteSpace(kw)) return;
        var list = await AppServices.Barang.QuickSearchAsync(kw, JenisFilter);
        if (kw != SearchText) return;
        Suggestions.Clear();
        foreach (var b in list) Suggestions.Add(b);
        SelectedSuggestion = Suggestions.FirstOrDefault();
        IsSuggestOpen = Suggestions.Count > 0;
    }

    /// <summary>Enter pada kolom scan: cocok persis barcode/SKU dulu, lalu saran teratas.</summary>
    private async Task ScanAsync()
    {
        var code = SearchText?.Trim();
        if (string.IsNullOrEmpty(code)) return;
        _searchTimer.Stop();

        if (IsSuggestOpen && SelectedSuggestion != null)
        {
            AddBarang(SelectedSuggestion);
            return;
        }
        var exact = await AppServices.Barang.GetByCodeAsync(code);
        if (exact != null)
        {
            AddBarang(exact);
            return;
        }
        await SearchBarangAsync();
        if (Suggestions.Count == 1) AddBarang(Suggestions[0]);
        else if (Suggestions.Count == 0) ToastService.Warning($"Barang \"{code}\" tidak ditemukan.");
    }

    public void AddBarang(Barang b)
    {
        if (b == null) return;
        if (IsSubsidi && !b.IsSubsidi)
        {
            ToastService.Warning($"{b.Nama} bukan barang subsidi. Gunakan mode Penjualan.");
            return;
        }
        if (!IsSubsidi && b.IsSubsidi)
        {
            ToastService.Warning($"{b.Nama} adalah barang subsidi. Gunakan mode Penyaluran Subsidi.");
            return;
        }
        var existing = Cart.FirstOrDefault(i => i.BarangId == b.BarangId);
        var qty = (existing?.Jumlah ?? 0) + 1;
        if (qty > b.StokSaatIni)
        {
            ToastService.Warning(b.StokSaatIni <= 0
                ? $"Stok {b.Nama} habis."
                : $"Stok {b.Nama} tidak cukup: tersedia {Fmt.Num(b.StokSaatIni)} {b.SatuanNama}.");
            return;
        }
        if (IsSubsidi && SelectedPenerima != null)
        {
            var kuota = SisaKuota.FirstOrDefault(k => k.BarangId == b.BarangId);
            if (kuota == null)
            {
                ToastService.Warning($"Kuota {b.Nama} untuk {SelectedPenerima.JenisLabel} belum ditetapkan.");
                return;
            }
            if (qty > kuota.Sisa)
            {
                ToastService.Warning($"Jumlah melebihi sisa kuota {SelectedPenerima.Nama}: sisa {Fmt.Num(kuota.Sisa)} {kuota.SatuanNama}.");
                return;
            }
        }
        if (existing != null)
        {
            existing.Jumlah = qty;
            SelectedItem = existing;
        }
        else
        {
            var item = new LineItem { Barang = b, Jumlah = 1, Harga = b.HargaUntuk(SelectedAnggota != null) };
            Cart.Add(item);
            SelectedItem = item;
        }
        SearchText = "";
        IsSuggestOpen = false;
    }

    private void ChangeQty(LineItem item, decimal delta)
    {
        var qty = item.Jumlah + delta;
        if (qty <= 0)
        {
            Cart.Remove(item);
            return;
        }
        if (qty > item.Barang.StokSaatIni)
        {
            ToastService.Warning($"Stok {item.Nama} tersedia {Fmt.Num(item.Barang.StokSaatIni)} {item.Satuan}.");
            return;
        }
        var kuota = IsSubsidi ? SisaKuota.FirstOrDefault(k => k.BarangId == item.BarangId) : null;
        if (kuota != null && qty > kuota.Sisa)
        {
            ToastService.Warning($"Sisa kuota {item.Nama}: {Fmt.Num(kuota.Sisa)} {kuota.SatuanNama}.");
            return;
        }
        item.Jumlah = qty;
    }

    /// <summary>Harga anggota/umum diterapkan otomatis saat pelanggan berganti (BR-10).</summary>
    private void Reprice()
    {
        foreach (var i in Cart) i.Harga = i.Barang.HargaUntuk(SelectedAnggota != null);
        RaiseTotals();
    }

    private async Task SearchAnggotaAsync()
    {
        var kw = AnggotaSearch;
        var list = await AppServices.Mitra.ListAnggotaAsync(kw, true);
        if (kw != AnggotaSearch) return;
        AnggotaResults.Clear();
        foreach (var a in list.Take(8)) AnggotaResults.Add(a);
    }

    private void SelectAnggota(Anggota a)
    {
        SelectedAnggota = a;
        IsAnggota = true;
        AnggotaSearch = "";
        AnggotaResults.Clear();
    }

    private async Task SearchPenerimaAsync()
    {
        var kw = PenerimaSearch;
        var list = await AppServices.Mitra.ListPenerimaAsync(kw, false);
        if (kw != PenerimaSearch) return;
        PenerimaResults.Clear();
        foreach (var p in list.Take(8)) PenerimaResults.Add(p);
    }

    private async Task SelectPenerimaAsync(PenerimaSubsidi p)
    {
        SelectedPenerima = p;
        PenerimaSearch = "";
        PenerimaResults.Clear();
        await LoadKuotaAsync();
        if (p.Status != "AKTIF")
            ToastService.Warning($"{p.Nama} berstatus nonaktif dan tidak dapat menerima barang subsidi.");
    }

    private async Task LoadKuotaAsync()
    {
        SisaKuota.Clear();
        if (SelectedPenerima == null) return;
        foreach (var k in await AppServices.Mitra.SisaKuotaAsync(SelectedPenerima.PenerimaId, Periode)) SisaKuota.Add(k);
    }

    private void ClearCart(bool confirm)
    {
        if (confirm && Cart.Count > 0 && !DialogService.Confirm("Kosongkan keranjang?", "Batalkan transaksi", "Kosongkan", true)) return;
        Cart.Clear();
        UangText = "";
        SearchText = "";
    }

    private async Task BayarAsync()
    {
        if (Cart.Count == 0) return;
        if (IsSubsidi && SelectedPenerima == null)
        {
            ToastService.Warning("Cari dan pilih penerima subsidi terlebih dahulu (NIK atau nama).");
            return;
        }
        if (IsAnggota && SelectedAnggota == null)
        {
            ToastService.Warning("Pilih anggota, atau ubah pelanggan menjadi Umum.");
            return;
        }
        if (string.IsNullOrWhiteSpace(UangText)) UangText = Total.ToString("0", Fmt.Id);   // uang pas
        if (!Num.TryParse(UangText, out var uang))
        {
            ToastService.Warning("Jumlah uang diterima tidak valid.");
            return;
        }
        if (uang < Total)
        {
            ToastService.Warning($"Uang diterima kurang {Fmt.Rp(Total - uang)}.");
            return;
        }

        IsBusy = true;
        try
        {
            var result = await AppServices.Penjualan.SaveAsync(new SaleRequest
            {
                Jenis = Mode,
                AnggotaId = IsSubsidi ? null : SelectedAnggota?.AnggotaId,
                PenerimaId = IsSubsidi ? SelectedPenerima.PenerimaId : null,
                Items = Cart.Select(i => (i.BarangId, i.Jumlah)).ToList(),
                UangDiterima = uang
            });
            _lastNomor = result.Receipt.NomorDokumen;
            ToastService.Success($"{result.Receipt.NomorDokumen} tersimpan. Kembalian {Fmt.Rp(result.Receipt.Kembalian)}.");
            Cart.Clear();
            UangText = "";
            if (IsSubsidi) await LoadKuotaAsync();
            DialogService.ShowReceipt(result.Receipt);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private int _lastId;

    private async Task CetakUlangAsync()
    {
        var list = await AppServices.Penjualan.ListAsync(DateTime.Today, DateTime.Today, null, _lastNomor);
        var row = list.FirstOrDefault();
        if (row == null) return;
        _lastId = row.Id;
        var receipt = await AppServices.Penjualan.ReceiptAsync(_lastId);
        if (receipt != null) DialogService.ShowReceipt(receipt);
    }

    // ---- Pintasan ----
    public void OnNew() => ClearCart(true);
    public void OnSave() => BayarCommand.Execute(null);

    public void OnEscape()
    {
        if (IsSuggestOpen) IsSuggestOpen = false;
        else SearchText = "";
    }
}

/// <summary>Parsing angka berformat Indonesia: "75.000" → 75000, "1,5" → 1.5.</summary>
public static class Num
{
    public static bool TryParse(string s, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim().Replace("Rp", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "");
        return decimal.TryParse(s, System.Globalization.NumberStyles.Number, Fmt.Id, out value);
    }

    public static string Format(decimal v) => v.ToString("0.##", Fmt.Id);
    public static string Format(decimal? v) => v.HasValue ? Format(v.Value) : "";
}
