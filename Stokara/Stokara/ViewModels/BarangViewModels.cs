using System.Collections.ObjectModel;
using System.Windows.Input;
using Stokara.Helpers;
using Stokara.Infrastructure;
using Stokara.Models;
using Stokara.Repositories;
using Stokara.Services;
using Stokara.Views;

namespace Stokara.ViewModels;

/// <summary>Daftar barang dengan pencarian, filter, urut, pagination (FR-08) dan panel kartu stok (FR-15).</summary>
public sealed class BarangViewModel : ViewModelBase, IShortcutTarget
{
    private string _keyword = "";
    private LookupItem _kategori;
    private string _jenis = "SEMUA";
    private string _status = "SEMUA";
    private bool _includeInactive;
    private int _page = 1;
    private int _total;
    private Barang _selected;
    private string _sortBy = "Nama";
    private bool _sortDesc;
    private DateTime _kartuDari = DateTime.Today.AddDays(-30);
    private DateTime _kartuSampai = DateTime.Today;
    private decimal _saldoAwal;
    private bool _detailOpen = true;
    private readonly System.Windows.Threading.DispatcherTimer _searchTimer;

    public BarangViewModel()
    {
        _searchTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchTimer.Tick += async (_, _) =>
        {
            _searchTimer.Stop();
            _page = 1;
            try { await SearchAsync(); } catch (Exception ex) { ErrorHandler.Handle(ex); }
        };
        SearchCommand = new AsyncRelayCommand(async () => { _page = 1; await SearchAsync(); });
        PrevCommand = new AsyncRelayCommand(async () => { _page--; await SearchAsync(); }, () => _page > 1);
        NextCommand = new AsyncRelayCommand(async () => { _page++; await SearchAsync(); }, () => _page < TotalPages);
        AddCommand = new AsyncRelayCommand(() => OpenFormAsync(null), () => CanEdit);
        EditCommand = new AsyncRelayCommand(() => OpenFormAsync(Selected), () => CanEdit && Selected != null);
        ToggleActiveCommand = new AsyncRelayCommand(ToggleActiveAsync, () => CanDeactivate && Selected != null);
        KartuStokCommand = new AsyncRelayCommand(LoadKartuAsync, () => Selected != null);
        SortCommand = new AsyncRelayCommand(async p =>
        {
            var col = p as string;
            if (col == _sortBy) _sortDesc = !_sortDesc;
            else { _sortBy = col; _sortDesc = false; }
            await SearchAsync();
        });
        ToggleDetailCommand = new RelayCommand(() => DetailOpen = !DetailOpen);
    }

    public ICommand SearchCommand { get; }
    public ICommand PrevCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand ToggleActiveCommand { get; }
    public ICommand KartuStokCommand { get; }
    public ICommand SortCommand { get; }
    public ICommand ToggleDetailCommand { get; }

    public bool CanEdit => Session.Is(Roles.Manajer, Roles.Gudang);
    public bool CanDeactivate => Session.Is(Roles.Manajer);
    public bool ShowHpp => Session.Is(Roles.Manajer, Roles.Pengurus);

    public ObservableCollection<Barang> Items { get; } = new();
    public ObservableCollection<LookupItem> Kategori { get; } = new();
    public ObservableCollection<MutasiRow> Kartu { get; } = new();

    public List<KeyValuePair<string, string>> JenisOptions { get; } = new()
    {
        new("SEMUA", "Semua jenis"), new("BEBAS", "Barang bebas"), new("SUBSIDI", "Subsidi"), new("TITIPAN", "Titipan")
    };

    public List<KeyValuePair<string, string>> StatusOptions { get; } = new()
    {
        new("SEMUA", "Semua status"), new("Aman", "Aman"), new("Menipis", "Menipis"), new("Habis", "Habis")
    };

    public string Keyword
    {
        get => _keyword;
        set { if (SetProperty(ref _keyword, value)) { _searchTimer.Stop(); _searchTimer.Start(); } }
    }

    public LookupItem SelectedKategori
    {
        get => _kategori;
        set { if (SetProperty(ref _kategori, value)) _searchTimer.Start(); }
    }

    public string Jenis
    {
        get => _jenis;
        set { if (SetProperty(ref _jenis, value)) _searchTimer.Start(); }
    }

    public string Status
    {
        get => _status;
        set { if (SetProperty(ref _status, value)) _searchTimer.Start(); }
    }

    public bool IncludeInactive
    {
        get => _includeInactive;
        set { if (SetProperty(ref _includeInactive, value)) _searchTimer.Start(); }
    }

    public int Page => _page;
    public int Total { get => _total; private set => SetProperty(ref _total, value); }
    public int PageSize => AppServices.Settings.UkuranHalaman;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    public string PageInfo => Total == 0 ? "Tidak ada data" :
        $"Halaman {_page} dari {TotalPages} · {Total:N0} barang";

    public Barang Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                Kartu.Clear();
                OnPropertyChanged(nameof(SaldoAkhir));
                OnPropertyChanged(nameof(ToggleActiveLabel));
                if (value != null && DetailOpen) _ = SafeKartu();
            }
        }
    }

    public string ToggleActiveLabel => Selected?.IsActive == false ? "Aktifkan" : "Nonaktifkan";

    public bool DetailOpen
    {
        get => _detailOpen;
        set => SetProperty(ref _detailOpen, value);
    }

    public DateTime KartuDari { get => _kartuDari; set { if (SetProperty(ref _kartuDari, value)) ScheduleReload(LoadKartuAsync); } }
    public DateTime KartuSampai { get => _kartuSampai; set { if (SetProperty(ref _kartuSampai, value)) ScheduleReload(LoadKartuAsync); } }
    public decimal SaldoAwal { get => _saldoAwal; private set => SetProperty(ref _saldoAwal, value); }
    public decimal SaldoAkhir => Kartu.Count > 0 ? Kartu[^1].SaldoSetelah : SaldoAwal;

    public override async Task LoadAsync()
    {
        Kategori.Clear();
        Kategori.Add(new LookupItem { Id = 0, Nama = "Semua kategori" });
        foreach (var k in await AppServices.Mitra.ListLookupAsync("Kategori")) Kategori.Add(k);
        _kategori = Kategori[0];
        OnPropertyChanged(nameof(SelectedKategori));
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        var keepId = Selected?.BarangId;
        var (items, total) = await AppServices.Barang.SearchAsync(new BarangFilter
        {
            Keyword = Keyword,
            KategoriId = SelectedKategori == null || SelectedKategori.Id == 0 ? null : SelectedKategori.Id,
            Jenis = Jenis,
            StatusStok = Status,
            IncludeInactive = IncludeInactive,
            Page = _page,
            PageSize = PageSize,
            SortBy = _sortBy,
            SortDesc = _sortDesc
        });
        if (_page > 1 && items.Count == 0 && total > 0)
        {
            _page = 1;
            await SearchAsync();
            return;
        }
        Items.Clear();
        foreach (var b in items) Items.Add(b);
        Total = total;
        OnPropertyChanged(nameof(Page));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageInfo));
        Selected = Items.FirstOrDefault(b => b.BarangId == keepId) ?? Items.FirstOrDefault();
    }

    private async Task SafeKartu()
    {
        try { await LoadKartuAsync(); } catch (Exception ex) { ErrorHandler.Handle(ex); }
    }

    private async Task LoadKartuAsync()
    {
        if (Selected == null) return;
        var (saldoAwal, rows) = await AppServices.Barang.KartuStokAsync(Selected.BarangId, KartuDari, KartuSampai);
        SaldoAwal = saldoAwal;
        Kartu.Clear();
        foreach (var r in rows) Kartu.Add(r);
        OnPropertyChanged(nameof(SaldoAkhir));
    }

    private async Task OpenFormAsync(Barang barang)
    {
        var vm = new BarangFormViewModel(barang);
        await vm.LoadAsync();
        var win = new BarangFormWindow { DataContext = vm, Owner = System.Windows.Application.Current.MainWindow };
        vm.CloseRequested += ok => { win.DialogResult = ok; };
        if (win.ShowDialog() == true)
        {
            await SearchAsync();
            Selected = Items.FirstOrDefault(b => b.BarangId == vm.SavedId) ?? Selected;
        }
    }

    private async Task ToggleActiveAsync()
    {
        var b = Selected;
        var aktif = !b.IsActive;
        var msg = aktif
            ? $"Aktifkan kembali {b.Nama}?"
            : $"Nonaktifkan {b.Nama}? Barang tidak akan muncul di kasir, tetapi riwayatnya tetap tersimpan (soft delete).";
        if (!DialogService.Confirm(msg, aktif ? "Aktifkan barang" : "Nonaktifkan barang", aktif ? "Aktifkan" : "Nonaktifkan", !aktif)) return;
        await AppServices.Barang.SetActiveAsync(b, aktif);
        ToastService.Success($"{b.Nama} {(aktif ? "diaktifkan" : "dinonaktifkan")}.");
        await SearchAsync();
    }

    public void OnNew() { if (CanEdit) AddCommand.Execute(null); }
    public void OnSave() { }
    public void OnEscape() => Keyword = "";
}

/// <summary>Form tambah/ubah barang dengan validasi inline (FR-09, FR-10, A-16).</summary>
public sealed class BarangFormViewModel : ViewModelBase
{
    private readonly Barang _original;
    private string _sku = "", _barcode = "", _nama = "";
    private LookupItem _kategori, _satuan;
    private string _kepemilikan = "MILIK";
    private Penitip _penitip;
    private bool _isSubsidi;
    private string _hargaUmum = "0", _hargaAnggota = "0", _het = "", _hargaTitip = "", _stokMin = "0";

    public event Action<bool> CloseRequested;
    public int SavedId { get; private set; }

    public BarangFormViewModel(Barang barang)
    {
        _original = barang;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false));
        if (barang != null)
        {
            _sku = barang.SKU;
            _barcode = barang.Barcode ?? "";
            _nama = barang.Nama;
            _kepemilikan = barang.Kepemilikan;
            _isSubsidi = barang.IsSubsidi;
            _hargaUmum = Num.Format(barang.HargaUmum);
            _hargaAnggota = Num.Format(barang.HargaAnggota);
            _het = Num.Format(barang.HET);
            _hargaTitip = Num.Format(barang.HargaTitip);
            _stokMin = Num.Format(barang.StokMinimum);
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public string Judul => _original == null ? "Tambah Barang" : "Ubah Barang";
    public string Subjudul => _original == null
        ? "Stok awal dimasukkan lewat menu Stok Masuk (sumber Stok Awal), bukan dari form ini."
        : $"Stok saat ini {Fmt.Num(_original.StokSaatIni)} {_original.SatuanNama}. Stok hanya berubah lewat transaksi.";

    /// <summary>A-16: Petugas Gudang tidak boleh mengubah harga, HET, harga titip, atau jenis barang.</summary>
    public bool CanEditHarga => Session.Is(Roles.Manajer);
    public bool HargaReadOnly => !CanEditHarga;
    public string HargaInfo => CanEditHarga ? null : "Harga, HET, dan jenis barang hanya dapat diubah oleh Manajer.";

    public ObservableCollection<LookupItem> KategoriList { get; } = new();
    public ObservableCollection<LookupItem> SatuanList { get; } = new();
    public ObservableCollection<Penitip> PenitipList { get; } = new();

    public string SKU { get => _sku; set { if (SetProperty(ref _sku, value)) ValidateField(nameof(SKU)); } }
    public string Barcode { get => _barcode; set { if (SetProperty(ref _barcode, value)) ValidateField(nameof(Barcode)); } }
    public string Nama { get => _nama; set { if (SetProperty(ref _nama, value)) ValidateField(nameof(Nama)); } }
    public LookupItem Kategori { get => _kategori; set { if (SetProperty(ref _kategori, value)) ValidateField(nameof(Kategori)); } }
    public LookupItem Satuan { get => _satuan; set { if (SetProperty(ref _satuan, value)) ValidateField(nameof(Satuan)); } }

    public bool IsTitipan
    {
        get => _kepemilikan == "TITIPAN";
        set
        {
            _kepemilikan = value ? "TITIPAN" : "MILIK";
            if (value) IsSubsidi = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsMilik));
            ValidateAll();
        }
    }

    public bool IsMilik
    {
        get => !IsTitipan;
        set => IsTitipan = !value;
    }

    public Penitip Penitip { get => _penitip; set { if (SetProperty(ref _penitip, value)) ValidateField(nameof(Penitip)); } }

    public bool IsSubsidi
    {
        get => _isSubsidi;
        set
        {
            if (!SetProperty(ref _isSubsidi, value)) return;
            if (value && IsTitipan) IsTitipan = false;
            if (value) HargaAnggota = HargaUmum;
            OnPropertyChanged(nameof(HargaAnggotaEnabled));
            ValidateAll();
        }
    }

    public bool HargaAnggotaEnabled => CanEditHarga && !IsSubsidi;

    public string HargaUmum
    {
        get => _hargaUmum;
        set
        {
            if (!SetProperty(ref _hargaUmum, value)) return;
            if (IsSubsidi) { _hargaAnggota = value; OnPropertyChanged(nameof(HargaAnggota)); }
            ValidateAll();
        }
    }

    public string HargaAnggota { get => _hargaAnggota; set { if (SetProperty(ref _hargaAnggota, value)) ValidateAll(); } }
    public string HET { get => _het; set { if (SetProperty(ref _het, value)) ValidateAll(); } }
    public string HargaTitip { get => _hargaTitip; set { if (SetProperty(ref _hargaTitip, value)) ValidateAll(); } }
    public string StokMinimum { get => _stokMin; set { if (SetProperty(ref _stokMin, value)) ValidateAll(); } }

    public string MarginInfo
    {
        get
        {
            if (!IsTitipan || !Num.TryParse(HargaUmum, out var jual) || !Num.TryParse(HargaTitip, out var titip)) return null;
            return $"Komisi koperasi per unit: {Fmt.Rp(jual - titip)}";
        }
    }

    public override async Task LoadAsync()
    {
        foreach (var k in await AppServices.Mitra.ListLookupAsync("Kategori")) KategoriList.Add(k);
        foreach (var s in await AppServices.Mitra.ListLookupAsync("Satuan")) SatuanList.Add(s);
        foreach (var p in await AppServices.Mitra.ListPenitipAsync(null, true)) PenitipList.Add(p);
        if (_original != null)
        {
            _kategori = KategoriList.FirstOrDefault(k => k.Id == _original.KategoriId);
            _satuan = SatuanList.FirstOrDefault(s => s.Id == _original.SatuanId);
            _penitip = PenitipList.FirstOrDefault(p => p.PenitipId == _original.PenitipId);
            OnPropertyChanged(nameof(Kategori));
            OnPropertyChanged(nameof(Satuan));
            OnPropertyChanged(nameof(Penitip));
        }
    }

    /// <summary>Membangun objek Barang dari isian; field angka yang tidak valid menjadi error.</summary>
    private Barang Build(out Dictionary<string, string> errorsOut)
    {
        var errors = new Dictionary<string, string>();
        errorsOut = errors;
        decimal Parse(string text, string field, bool optional = false)
        {
            if (optional && string.IsNullOrWhiteSpace(text)) return -1;
            if (!Num.TryParse(text, out var v)) { errors[field] = "Masukkan angka (pemisah ribuan titik, desimal koma)."; return 0; }
            return v;
        }
        var b = new Barang
        {
            BarangId = _original?.BarangId ?? 0,
            SKU = SKU,
            Barcode = Barcode,
            Nama = Nama,
            KategoriId = Kategori?.Id ?? 0,
            SatuanId = Satuan?.Id ?? 0,
            Kepemilikan = _kepemilikan,
            PenitipId = IsTitipan ? Penitip?.PenitipId : null,
            HargaUmum = Parse(HargaUmum, nameof(HargaUmum)),
            HargaAnggota = Parse(HargaAnggota, nameof(HargaAnggota)),
            StokMinimum = Parse(StokMinimum, nameof(StokMinimum)),
            IsSubsidi = IsSubsidi,
            StokSaatIni = _original?.StokSaatIni ?? 0
        };
        var het = Parse(HET, nameof(HET), true);
        b.HET = het < 0 ? null : het;
        var titip = Parse(HargaTitip, nameof(HargaTitip), true);
        b.HargaTitip = titip < 0 ? null : titip;
        if (b.IsSubsidi) b.HargaAnggota = b.HargaUmum;
        foreach (var e in BarangService.Validate(b))
        {
            var key = e.Key switch
            {
                nameof(Barang.KategoriId) => nameof(Kategori),
                nameof(Barang.SatuanId) => nameof(Satuan),
                nameof(Barang.PenitipId) => nameof(Penitip),
                nameof(Barang.Kepemilikan) => nameof(IsTitipan),
                _ => e.Key
            };
            errors.TryAdd(key, e.Value);
        }
        return b;
    }

    private static readonly string[] Fields =
        { nameof(SKU), nameof(Barcode), nameof(Nama), nameof(Kategori), nameof(Satuan), nameof(Penitip),
          nameof(HargaUmum), nameof(HargaAnggota), nameof(HET), nameof(HargaTitip), nameof(StokMinimum), nameof(IsTitipan) };

    private bool _touchedAll;

    private void ValidateField(string field)
    {
        Build(out var errors);
        Validate(field, !errors.TryGetValue(field, out var msg), msg);
        OnPropertyChanged(nameof(MarginInfo));
    }

    private void ValidateAll()
    {
        Build(out var errors);
        foreach (var f in Fields)
        {
            // Sebelum tombol Simpan ditekan, hanya tampilkan error untuk field yang sudah berisi.
            if (!_touchedAll && !errors.ContainsKey(f)) { ClearError(f); continue; }
            if (!_touchedAll && IsEmptyField(f)) continue;
            Validate(f, !errors.TryGetValue(f, out var msg), msg);
        }
        OnPropertyChanged(nameof(MarginInfo));
    }

    private bool IsEmptyField(string f) => f switch
    {
        nameof(HET) => string.IsNullOrWhiteSpace(HET),
        nameof(HargaTitip) => string.IsNullOrWhiteSpace(HargaTitip),
        nameof(Penitip) => Penitip == null,
        _ => false
    };

    private async Task SaveAsync()
    {
        _touchedAll = true;
        var b = Build(out var errors);
        foreach (var f in Fields) Validate(f, !errors.TryGetValue(f, out var msg), msg);
        if (errors.Count > 0)
        {
            ToastService.Warning("Periksa kembali isian yang ditandai merah.");
            return;
        }
        IsBusy = true;
        try
        {
            SavedId = await AppServices.Barang.SaveAsync(b);
            ToastService.Success($"{b.Nama} tersimpan.");
            CloseRequested?.Invoke(true);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
