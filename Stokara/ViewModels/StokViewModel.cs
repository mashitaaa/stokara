using System.Collections.ObjectModel;
using System.Windows.Input;
using Stokara.Helpers;
using Stokara.Infrastructure;
using Stokara.Models;
using Stokara.Services;

namespace Stokara.ViewModels;

/// <summary>
/// Pemilihan barang dengan pencarian (autocomplete) untuk form multi-item penerimaan/pengeluaran.
/// </summary>
public sealed class BarangPicker : ObservableObject
{
    private string _text = "";
    private Barang _selected;
    private bool _isOpen;
    private readonly Func<string> _jenis;
    private readonly Func<int?> _penitip;
    private readonly System.Windows.Threading.DispatcherTimer _timer;

    public BarangPicker(Func<string> jenis, Func<int?> penitip = null)
    {
        _jenis = jenis;
        _penitip = penitip ?? (() => null);
        _timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += async (_, _) =>
        {
            _timer.Stop();
            try
            {
                var kw = Text;
                var list = await AppServices.Barang.QuickSearchAsync(kw, _jenis(), _penitip());
                if (kw != Text) return;
                Results.Clear();
                foreach (var b in list) Results.Add(b);
                IsOpen = Results.Count > 0;
            }
            catch (Exception ex) { ErrorHandler.Handle(ex); }
        };
        PickCommand = new RelayCommand(p => { if (p is Barang b) Pick(b); });
    }

    public ObservableCollection<Barang> Results { get; } = new();
    public ICommand PickCommand { get; }
    public event Action<Barang> Picked;

    public string Text
    {
        get => _text;
        set
        {
            if (!SetProperty(ref _text, value)) return;
            if (Selected != null && value != Selected.Nama) Selected = null;
            _timer.Stop();
            if (string.IsNullOrWhiteSpace(value)) { Results.Clear(); IsOpen = false; }
            else if (Selected == null) _timer.Start();
        }
    }

    public Barang Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    public bool IsOpen
    {
        get => _isOpen;
        set => SetProperty(ref _isOpen, value);
    }

    public void Pick(Barang b)
    {
        Selected = b;
        _text = b.Nama;
        OnPropertyChanged(nameof(Text));
        IsOpen = false;
        Picked?.Invoke(b);
    }

    public void Reset()
    {
        Selected = null;
        _text = "";
        OnPropertyChanged(nameof(Text));
        Results.Clear();
        IsOpen = false;
    }
}

/// <summary>Stok Masuk (penerimaan, FR-13) dan Stok Keluar non-penjualan (retur/rusak, FR-14).</summary>
public sealed class StokViewModel : ViewModelBase, IShortcutTarget
{
    // ---------- Daftar ----------
    private DateTime _dari = DateTime.Today.AddDays(-30);
    private DateTime _sampai = DateTime.Today;
    private TransaksiRow _selectedPenerimaan;
    private TransaksiRow _selectedPengeluaran;
    private int _tab;

    // ---------- Form penerimaan ----------
    private bool _formOpen;
    private string _sumber = "PEMBELIAN";
    private Pemasok _pemasok;
    private Penitip _penitip;
    private string _suratJalan = "", _catatan = "";
    private string _qty = "", _harga = "";
    private DateTime? _exp;

    // ---------- Form pengeluaran ----------
    private bool _formKeluarOpen;
    private string _jenisKeluar = "RUSAK";
    private Pemasok _pemasokKeluar;
    private Penitip _penitipKeluar;
    private string _alasan = "";
    private string _qtyKeluar = "";

    public StokViewModel()
    {
        Picker = new BarangPicker(() => StokService.JenisBarangUntukSumber(Sumber), () => Sumber == "KONSINYASI" ? Penitip?.PenitipId : null);
        Picker.Picked += b =>
        {
            Harga = Num.Format(Sumber == "KONSINYASI" ? b.HargaTitip ?? 0 : b.HargaPokokRata > 0 ? Math.Round(b.HargaPokokRata, 0) : 0);
            Qty = "1";
        };
        PickerKeluar = new BarangPicker(() => JenisKeluar == "RETUR_PENITIP" ? "TITIPAN" : JenisKeluar == "RETUR_PEMASOK" ? "MILIK" : null,
            () => JenisKeluar == "RETUR_PENITIP" ? PenitipKeluar?.PenitipId : null);
        PickerKeluar.Picked += _ => QtyKeluar = "1";

        SearchCommand = new AsyncRelayCommand(LoadListsAsync);
        NewPenerimaanCommand = new RelayCommand(() => { ResetForm(); FormOpen = true; Tab = 0; }, () => CanInput);
        AddItemCommand = new RelayCommand(AddItem);
        RemoveItemCommand = new RelayCommand(p => { if (p is LineItem i) Items.Remove(i); });
        SavePenerimaanCommand = new AsyncRelayCommand(SavePenerimaanAsync, () => Items.Count > 0);
        CancelFormCommand = new RelayCommand(() => FormOpen = false);
        VoidPenerimaanCommand = new AsyncRelayCommand(VoidPenerimaanAsync,
            () => SelectedPenerimaan?.Status == "AKTIF" && Session.Is(Roles.Manajer));

        NewPengeluaranCommand = new RelayCommand(() => { ResetFormKeluar(); FormKeluarOpen = true; Tab = 1; }, () => CanInput);
        AddItemKeluarCommand = new RelayCommand(AddItemKeluar);
        RemoveItemKeluarCommand = new RelayCommand(p => { if (p is LineItem i) ItemsKeluar.Remove(i); });
        SavePengeluaranCommand = new AsyncRelayCommand(SavePengeluaranAsync, () => ItemsKeluar.Count > 0);
        CancelFormKeluarCommand = new RelayCommand(() => FormKeluarOpen = false);

        Items.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TotalPenerimaan));
    }

    public BarangPicker Picker { get; }
    public BarangPicker PickerKeluar { get; }

    public ICommand SearchCommand { get; }
    public ICommand NewPenerimaanCommand { get; }
    public ICommand AddItemCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand SavePenerimaanCommand { get; }
    public ICommand CancelFormCommand { get; }
    public ICommand VoidPenerimaanCommand { get; }
    public ICommand NewPengeluaranCommand { get; }
    public ICommand AddItemKeluarCommand { get; }
    public ICommand RemoveItemKeluarCommand { get; }
    public ICommand SavePengeluaranCommand { get; }
    public ICommand CancelFormKeluarCommand { get; }

    public bool CanInput => Session.Is(Roles.Manajer, Roles.Gudang);
    public bool CanVoid => Session.Is(Roles.Manajer);

    public int Tab { get => _tab; set => SetProperty(ref _tab, value); }
    public DateTime Dari { get => _dari; set { if (SetProperty(ref _dari, value)) ScheduleReload(LoadListsAsync); } }
    public DateTime Sampai { get => _sampai; set { if (SetProperty(ref _sampai, value)) ScheduleReload(LoadListsAsync); } }

    public ObservableCollection<TransaksiRow> Penerimaan { get; } = new();
    public ObservableCollection<TransaksiRow> Pengeluaran { get; } = new();
    public ObservableCollection<TransaksiDetailRow> Detail { get; } = new();
    public ObservableCollection<TransaksiDetailRow> DetailKeluar { get; } = new();
    public ObservableCollection<Pemasok> PemasokList { get; } = new();
    public ObservableCollection<Penitip> PenitipList { get; } = new();

    public TransaksiRow SelectedPenerimaan
    {
        get => _selectedPenerimaan;
        set { if (SetProperty(ref _selectedPenerimaan, value)) _ = LoadDetailAsync(); }
    }

    public TransaksiRow SelectedPengeluaran
    {
        get => _selectedPengeluaran;
        set { if (SetProperty(ref _selectedPengeluaran, value)) _ = LoadDetailKeluarAsync(); }
    }

    // ================= Form penerimaan =================
    public List<KeyValuePair<string, string>> SumberOptions { get; } =
        StokService.SumberPenerimaan.Select(s => new KeyValuePair<string, string>(s, Fmt.SumberLabel(s))).ToList();

    public bool FormOpen { get => _formOpen; set => SetProperty(ref _formOpen, value); }

    public string Sumber
    {
        get => _sumber;
        set
        {
            if (!SetProperty(ref _sumber, value)) return;
            if (Items.Count > 0)
            {
                Items.Clear();
                ToastService.Info("Daftar barang dikosongkan karena sumber penerimaan berubah.");
            }
            Picker.Reset();
            OnPropertyChanged(nameof(NeedPemasok));
            OnPropertyChanged(nameof(NeedPenitip));
            OnPropertyChanged(nameof(HargaLabel));
            OnPropertyChanged(nameof(SumberInfo));
        }
    }

    public bool NeedPemasok => Sumber is "PEMBELIAN" or "SUBSIDI";
    public bool NeedPenitip => Sumber == "KONSINYASI";
    public string HargaLabel => Sumber == "KONSINYASI" ? "Harga titip / unit" : "Harga beli / unit";

    public string SumberInfo => Sumber switch
    {
        "PEMBELIAN" => "Barang bebas milik koperasi. Harga beli memperbarui HPP rata-rata tertimbang.",
        "SUBSIDI" => "Hanya barang subsidi (pupuk, LPG, beras SPHP) dari pemasok resmi.",
        "KONSINYASI" => "Barang titipan penitip. Tidak menambah nilai persediaan koperasi.",
        _ => "Untuk memasukkan stok awal saat gerai mulai memakai STOKARA."
    };

    public Pemasok Pemasok { get => _pemasok; set => SetProperty(ref _pemasok, value); }

    public Penitip Penitip
    {
        get => _penitip;
        set
        {
            if (!SetProperty(ref _penitip, value)) return;
            if (Items.Count > 0) Items.Clear();
            Picker.Reset();
        }
    }

    public string SuratJalan { get => _suratJalan; set => SetProperty(ref _suratJalan, value); }
    public string Catatan { get => _catatan; set => SetProperty(ref _catatan, value); }

    public string Qty
    {
        get => _qty;
        set { if (SetProperty(ref _qty, value)) Validate(nameof(Qty), value == "" || (Num.TryParse(value, out var q) && q > 0), "Jumlah harus > 0"); }
    }

    public string Harga
    {
        get => _harga;
        set { if (SetProperty(ref _harga, value)) Validate(nameof(Harga), value == "" || (Num.TryParse(value, out var h) && h >= 0), "Harga tidak valid"); }
    }

    public DateTime? Kedaluwarsa { get => _exp; set => SetProperty(ref _exp, value); }

    public ObservableCollection<LineItem> Items { get; } = new();
    public decimal TotalPenerimaan => Items.Sum(i => i.Subtotal);

    // ================= Form pengeluaran =================
    public List<KeyValuePair<string, string>> JenisKeluarOptions { get; } =
        StokService.JenisPengeluaran.Select(s => new KeyValuePair<string, string>(s, Fmt.SumberLabel(s))).ToList();

    public bool FormKeluarOpen { get => _formKeluarOpen; set => SetProperty(ref _formKeluarOpen, value); }

    public string JenisKeluar
    {
        get => _jenisKeluar;
        set
        {
            if (!SetProperty(ref _jenisKeluar, value)) return;
            ItemsKeluar.Clear();
            PickerKeluar.Reset();
            OnPropertyChanged(nameof(NeedPemasokKeluar));
            OnPropertyChanged(nameof(NeedPenitipKeluar));
        }
    }

    public bool NeedPemasokKeluar => JenisKeluar == "RETUR_PEMASOK";
    public bool NeedPenitipKeluar => JenisKeluar == "RETUR_PENITIP";
    public Pemasok PemasokKeluar { get => _pemasokKeluar; set => SetProperty(ref _pemasokKeluar, value); }

    public Penitip PenitipKeluar
    {
        get => _penitipKeluar;
        set
        {
            if (!SetProperty(ref _penitipKeluar, value)) return;
            ItemsKeluar.Clear();
            PickerKeluar.Reset();
        }
    }

    public string Alasan { get => _alasan; set => SetProperty(ref _alasan, value); }

    public string QtyKeluar
    {
        get => _qtyKeluar;
        set { if (SetProperty(ref _qtyKeluar, value)) Validate(nameof(QtyKeluar), value == "" || (Num.TryParse(value, out var q) && q > 0), "Jumlah harus > 0"); }
    }

    public ObservableCollection<LineItem> ItemsKeluar { get; } = new();

    // ================= Logika =================

    public override async Task LoadAsync()
    {
        foreach (var p in await AppServices.Mitra.ListPemasokAsync(null, true)) PemasokList.Add(p);
        foreach (var p in await AppServices.Mitra.ListPenitipAsync(null, true)) PenitipList.Add(p);
        await LoadListsAsync();
    }

    private async Task LoadListsAsync()
    {
        if (Sampai < Dari) { ToastService.Warning("Tanggal akhir tidak boleh sebelum tanggal awal."); return; }
        Penerimaan.Clear();
        foreach (var r in await AppServices.Stok.ListPenerimaanAsync(Dari, Sampai, null, null)) Penerimaan.Add(r);
        Pengeluaran.Clear();
        foreach (var r in await AppServices.Stok.ListPengeluaranAsync(Dari, Sampai, null)) Pengeluaran.Add(r);
        SelectedPenerimaan = Penerimaan.FirstOrDefault();
        SelectedPengeluaran = Pengeluaran.FirstOrDefault();
    }

    private async Task LoadDetailAsync()
    {
        Detail.Clear();
        if (SelectedPenerimaan == null) return;
        try { foreach (var d in await AppServices.Stok.PenerimaanDetailAsync(SelectedPenerimaan.Id)) Detail.Add(d); }
        catch (Exception ex) { ErrorHandler.Handle(ex); }
    }

    private async Task LoadDetailKeluarAsync()
    {
        DetailKeluar.Clear();
        if (SelectedPengeluaran == null) return;
        try { foreach (var d in await AppServices.Stok.PengeluaranDetailAsync(SelectedPengeluaran.Id)) DetailKeluar.Add(d); }
        catch (Exception ex) { ErrorHandler.Handle(ex); }
    }

    private void ResetForm()
    {
        Items.Clear();
        Picker.Reset();
        SuratJalan = Catatan = Qty = Harga = "";
        Kedaluwarsa = null;
        Pemasok = null;
        _penitip = null;
        OnPropertyChanged(nameof(Penitip));
    }

    private void AddItem()
    {
        var b = Picker.Selected;
        if (b == null) { ToastService.Warning("Cari dan pilih barang terlebih dahulu."); return; }
        if (!Num.TryParse(Qty, out var q) || q <= 0) { ToastService.Warning("Jumlah harus lebih dari 0."); return; }
        if (!Num.TryParse(string.IsNullOrWhiteSpace(Harga) ? "0" : Harga, out var h) || h < 0) { ToastService.Warning("Harga tidak valid."); return; }
        var existing = Items.FirstOrDefault(i => i.BarangId == b.BarangId);
        if (existing != null)
        {
            existing.Jumlah += q;
            existing.Harga = h;
        }
        else
        {
            Items.Add(new LineItem { Barang = b, Jumlah = q, Harga = h, TanggalKedaluwarsa = Kedaluwarsa });
        }
        OnPropertyChanged(nameof(TotalPenerimaan));
        Picker.Reset();
        Qty = Harga = "";
        Kedaluwarsa = null;
    }

    private async Task SavePenerimaanAsync()
    {
        if (NeedPemasok && Pemasok == null) { ToastService.Warning("Pilih pemasok."); return; }
        if (NeedPenitip && Penitip == null) { ToastService.Warning("Pilih penitip."); return; }
        if (!DialogService.Confirm($"Simpan penerimaan {Items.Count} barang senilai {Fmt.Rp(TotalPenerimaan)}? Stok akan bertambah.",
                "Simpan penerimaan", "Simpan")) return;
        var nomor = await AppServices.Stok.SavePenerimaanAsync(new PenerimaanRequest
        {
            Sumber = Sumber,
            PemasokId = Pemasok?.PemasokId,
            PenitipId = Penitip?.PenitipId,
            NomorSuratJalan = SuratJalan,
            Catatan = Catatan,
            Items = Items.ToList()
        });
        ToastService.Success($"Penerimaan {nomor} tersimpan. Stok sudah bertambah.");
        FormOpen = false;
        ResetForm();
        await LoadListsAsync();
    }

    private async Task VoidPenerimaanAsync()
    {
        var row = SelectedPenerimaan;
        var alasan = DialogService.Prompt($"Penerimaan {row.NomorDokumen} akan dibatalkan dan stoknya dikurangi kembali.",
            "Batalkan penerimaan", "Alasan pembatalan", "", true, "Batalkan");
        if (alasan == null) return;
        await AppServices.Stok.VoidPenerimaanAsync(row.Id, alasan);
        ToastService.Success($"{row.NomorDokumen} dibatalkan.");
        await LoadListsAsync();
    }

    private void ResetFormKeluar()
    {
        ItemsKeluar.Clear();
        PickerKeluar.Reset();
        Alasan = QtyKeluar = "";
        PemasokKeluar = null;
        _penitipKeluar = null;
        OnPropertyChanged(nameof(PenitipKeluar));
    }

    private void AddItemKeluar()
    {
        var b = PickerKeluar.Selected;
        if (b == null) { ToastService.Warning("Cari dan pilih barang terlebih dahulu."); return; }
        if (!Num.TryParse(QtyKeluar, out var q) || q <= 0) { ToastService.Warning("Jumlah harus lebih dari 0."); return; }
        var total = (ItemsKeluar.FirstOrDefault(i => i.BarangId == b.BarangId)?.Jumlah ?? 0) + q;
        if (total > b.StokSaatIni) { ToastService.Warning($"Stok {b.Nama} hanya {Fmt.Num(b.StokSaatIni)} {b.SatuanNama}."); return; }
        var existing = ItemsKeluar.FirstOrDefault(i => i.BarangId == b.BarangId);
        if (existing != null) existing.Jumlah = total;
        else ItemsKeluar.Add(new LineItem { Barang = b, Jumlah = q });
        PickerKeluar.Reset();
        QtyKeluar = "";
    }

    private async Task SavePengeluaranAsync()
    {
        if (string.IsNullOrWhiteSpace(Alasan)) { ToastService.Warning("Alasan wajib diisi."); return; }
        if (NeedPemasokKeluar && PemasokKeluar == null) { ToastService.Warning("Pilih pemasok tujuan retur."); return; }
        if (NeedPenitipKeluar && PenitipKeluar == null) { ToastService.Warning("Pilih penitip tujuan retur."); return; }
        if (!DialogService.Confirm($"Keluarkan {ItemsKeluar.Count} barang sebagai {Fmt.SumberLabel(JenisKeluar)}? Stok akan berkurang.",
                "Simpan pengeluaran", "Simpan", true)) return;
        var nomor = await AppServices.Stok.SavePengeluaranAsync(new PengeluaranRequest
        {
            Jenis = JenisKeluar,
            PemasokId = PemasokKeluar?.PemasokId,
            PenitipId = PenitipKeluar?.PenitipId,
            Alasan = Alasan,
            Items = ItemsKeluar.ToList()
        });
        ToastService.Success($"{nomor} tersimpan. Stok sudah dikurangi.");
        FormKeluarOpen = false;
        ResetFormKeluar();
        await LoadListsAsync();
    }

    public void OnNew()
    {
        if (!CanInput) return;
        if (Tab == 1) NewPengeluaranCommand.Execute(null);
        else NewPenerimaanCommand.Execute(null);
    }

    public void OnSave()
    {
        if (FormOpen && Tab == 0) SavePenerimaanCommand.Execute(null);
        else if (FormKeluarOpen && Tab == 1) SavePengeluaranCommand.Execute(null);
    }

    public void OnEscape()
    {
        if (Tab == 0) FormOpen = false;
        else FormKeluarOpen = false;
    }
}
