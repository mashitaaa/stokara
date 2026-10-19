using System.Collections.ObjectModel;
using System.Windows.Input;
using Stokara.Helpers;
using Stokara.Infrastructure;
using Stokara.Models;
using Stokara.Services;

namespace Stokara.ViewModels;

/// <summary>Mitra: anggota, penerima subsidi &amp; aturan kuota, pemasok, penitip (FR-11, FR-12).</summary>
public sealed class MitraViewModel : ViewModelBase, IShortcutTarget
{
    private int _tab;
    private string _cariAnggota = "", _cariPenerima = "", _cariPemasok = "", _cariPenitip = "";
    private Anggota _selAnggota, _editAnggota;
    private PenerimaSubsidi _selPenerima, _editPenerima;
    private Pemasok _selPemasok, _editPemasok;
    private Penitip _selPenitip, _editPenitip;
    private string _periode;
    private AturanKuota _selAturan;
    private Barang _aturanBarang;
    private string _aturanJenis = "RUMAH_TANGGA";
    private string _aturanPeriode;
    private string _aturanJumlah = "";
    private int _aturanId;

    public MitraViewModel()
    {
        _periode = AppServices.Settings.PeriodeSekarang;
        _aturanPeriode = _periode;

        CariCommand = new AsyncRelayCommand(LoadTabAsync);
        NewAnggotaCommand = new AsyncRelayCommand(NewAnggotaAsync, () => CanAddAnggota);
        SaveAnggotaCommand = new AsyncRelayCommand(SaveAnggotaAsync, () => EditAnggota != null && AnggotaEditable);
        NewPenerimaCommand = new RelayCommand(() => EditPenerima = new PenerimaSubsidi(), () => IsManajer);
        SavePenerimaCommand = new AsyncRelayCommand(SavePenerimaAsync, () => EditPenerima != null && IsManajer);
        NewPemasokCommand = new RelayCommand(() => EditPemasok = new Pemasok(), () => IsManajer);
        SavePemasokCommand = new AsyncRelayCommand(SavePemasokAsync, () => EditPemasok != null && IsManajer);
        TogglePemasokCommand = new AsyncRelayCommand(TogglePemasokAsync, () => SelectedPemasok != null && IsManajer);
        NewPenitipCommand = new RelayCommand(() => EditPenitip = new Penitip(), () => IsManajer);
        SavePenitipCommand = new AsyncRelayCommand(SavePenitipAsync, () => EditPenitip != null && IsManajer);
        TogglePenitipCommand = new AsyncRelayCommand(TogglePenitipAsync, () => SelectedPenitip != null && IsManajer);

        LoadAturanCommand = new AsyncRelayCommand(LoadAturanAsync);
        NewAturanCommand = new RelayCommand(NewAturan, () => IsManajer);
        SaveAturanCommand = new AsyncRelayCommand(SaveAturanAsync, () => IsManajer);
        DeleteAturanCommand = new AsyncRelayCommand(DeleteAturanAsync, () => IsManajer && SelectedAturan != null);
        CopyAturanCommand = new AsyncRelayCommand(CopyAturanAsync, () => IsManajer);
    }

    // ---------------- Hak akses (matriks 4.1) ----------------
    public bool IsManajer => Session.Is(Roles.Manajer);
    public bool ShowAnggota => Session.Is(Roles.Manajer, Roles.Kasir, Roles.Pengurus);
    public bool ShowPenerima => Session.Is(Roles.Manajer, Roles.Kasir, Roles.Pengurus);
    public bool ShowPemasok => Session.Is(Roles.Manajer, Roles.Gudang, Roles.Pengurus);
    public bool ShowPenitip => Session.Is(Roles.Manajer, Roles.Gudang, Roles.Pengurus);
    public bool CanAddAnggota => Session.Is(Roles.Manajer, Roles.Kasir);
    public bool AnggotaEditable => IsManajer || (Session.Is(Roles.Kasir) && EditAnggota?.AnggotaId == 0);
    public bool AnggotaReadOnly => !AnggotaEditable;
    public bool ManajerReadOnly => !IsManajer;

    public ICommand CariCommand { get; }
    public ICommand NewAnggotaCommand { get; }
    public ICommand SaveAnggotaCommand { get; }
    public ICommand NewPenerimaCommand { get; }
    public ICommand SavePenerimaCommand { get; }
    public ICommand NewPemasokCommand { get; }
    public ICommand SavePemasokCommand { get; }
    public ICommand TogglePemasokCommand { get; }
    public ICommand NewPenitipCommand { get; }
    public ICommand SavePenitipCommand { get; }
    public ICommand TogglePenitipCommand { get; }
    public ICommand LoadAturanCommand { get; }
    public ICommand NewAturanCommand { get; }
    public ICommand SaveAturanCommand { get; }
    public ICommand DeleteAturanCommand { get; }
    public ICommand CopyAturanCommand { get; }

    public int Tab
    {
        get => _tab;
        set { if (SetProperty(ref _tab, value)) _ = SafeLoadTab(); }
    }

    public List<string> StatusOptions { get; } = new() { "AKTIF", "NONAKTIF" };
    public List<string> JenisPemasokOptions { get; } = new() { "BUMN", "BUMD", "LOKAL" };
    public List<KeyValuePair<string, string>> JenisPenerimaOptions { get; } = new()
    {
        new("PETANI", "Petani"), new("RUMAH_TANGGA", "Rumah Tangga"), new("USAHA_MIKRO", "Usaha Mikro"), new("LAINNYA", "Lainnya")
    };

    // ================= Anggota =================
    public ObservableCollection<Anggota> AnggotaList { get; } = new();
    public string CariAnggota { get => _cariAnggota; set => SetProperty(ref _cariAnggota, value); }

    public Anggota SelectedAnggota
    {
        get => _selAnggota;
        set { if (SetProperty(ref _selAnggota, value) && value != null) EditAnggota = CloneForView(value); }
    }

    public Anggota EditAnggota
    {
        get => _editAnggota;
        set
        {
            SetProperty(ref _editAnggota, value);
            OnPropertyChanged(nameof(AnggotaEditable));
            OnPropertyChanged(nameof(AnggotaReadOnly));
        }
    }

    private static Anggota CloneForView(Anggota a) => new()
    {
        AnggotaId = a.AnggotaId, NomorAnggota = a.NomorAnggota,
        NIK = Session.CanSeeFullNik ? a.NIK : a.NikTampil,   // NIK lengkap hanya untuk Manajer (NFR-06)
        Nama = a.Nama, Alamat = a.Alamat, Dusun = a.Dusun, Telepon = a.Telepon, Status = a.Status, TanggalBergabung = a.TanggalBergabung
    };

    private async Task NewAnggotaAsync()
    {
        SelectedAnggota = null;
        EditAnggota = new Anggota { NomorAnggota = await AppServices.Mitra.NextNomorAnggotaAsync() };
    }

    private async Task SaveAnggotaAsync()
    {
        var a = EditAnggota;
        await AppServices.Mitra.SaveAnggotaAsync(a);
        ToastService.Success($"Anggota {a.Nama} tersimpan.");
        await LoadAnggotaAsync(a.NomorAnggota);
    }

    private async Task LoadAnggotaAsync(string selectNomor = null)
    {
        AnggotaList.Clear();
        foreach (var a in await AppServices.Mitra.ListAnggotaAsync(CariAnggota)) AnggotaList.Add(a);
        SelectedAnggota = AnggotaList.FirstOrDefault(a => a.NomorAnggota == selectNomor) ?? AnggotaList.FirstOrDefault();
        if (SelectedAnggota == null) EditAnggota = null;
    }

    // ================= Penerima & kuota =================
    public ObservableCollection<PenerimaSubsidi> PenerimaList { get; } = new();
    public ObservableCollection<SisaKuota> SisaKuota { get; } = new();
    public ObservableCollection<AturanKuota> AturanList { get; } = new();
    public ObservableCollection<Barang> BarangSubsidi { get; } = new();
    public ObservableCollection<Anggota> AnggotaPilihan { get; } = new();
    public string CariPenerima { get => _cariPenerima; set => SetProperty(ref _cariPenerima, value); }

    public PenerimaSubsidi SelectedPenerima
    {
        get => _selPenerima;
        set
        {
            if (!SetProperty(ref _selPenerima, value) || value == null) return;
            EditPenerima = new PenerimaSubsidi
            {
                PenerimaId = value.PenerimaId, NIK = Session.CanSeeFullNik ? value.NIK : value.NikTampil, Nama = value.Nama,
                JenisPenerima = value.JenisPenerima, Dusun = value.Dusun, AnggotaId = value.AnggotaId, Status = value.Status
            };
            _ = LoadSisaAsync();
        }
    }

    public PenerimaSubsidi EditPenerima
    {
        get => _editPenerima;
        set
        {
            SetProperty(ref _editPenerima, value);
            OnPropertyChanged(nameof(EditPenerimaAnggota));
        }
    }

    public Anggota EditPenerimaAnggota
    {
        get => AnggotaPilihan.FirstOrDefault(a => a.AnggotaId == (EditPenerima?.AnggotaId ?? -1)) ?? AnggotaPilihan.FirstOrDefault();
        set
        {
            if (EditPenerima == null) return;
            EditPenerima.AnggotaId = value == null || value.AnggotaId == 0 ? null : value.AnggotaId;
            OnPropertyChanged();
        }
    }

    public string Periode { get => _periode; set => SetProperty(ref _periode, value); }

    private async Task LoadSisaAsync()
    {
        SisaKuota.Clear();
        if (SelectedPenerima == null) return;
        try { foreach (var k in await AppServices.Mitra.SisaKuotaAsync(SelectedPenerima.PenerimaId, Periode)) SisaKuota.Add(k); }
        catch (Exception ex) { ErrorHandler.Handle(ex); }
    }

    private async Task SavePenerimaAsync()
    {
        var p = EditPenerima;
        await AppServices.Mitra.SavePenerimaAsync(p);
        ToastService.Success($"Penerima {p.Nama} tersimpan.");
        await LoadPenerimaAsync(p.NIK);
    }

    private async Task LoadPenerimaAsync(string selectNik = null)
    {
        if (AnggotaPilihan.Count == 0 && IsManajer)
        {
            AnggotaPilihan.Add(new Anggota { AnggotaId = 0, Nama = "(bukan anggota)", NomorAnggota = "-" });
            foreach (var a in await AppServices.Mitra.ListAnggotaAsync(null, true)) AnggotaPilihan.Add(a);
        }
        PenerimaList.Clear();
        foreach (var p in await AppServices.Mitra.ListPenerimaAsync(CariPenerima)) PenerimaList.Add(p);
        SelectedPenerima = PenerimaList.FirstOrDefault(p => p.NIK == selectNik) ?? PenerimaList.FirstOrDefault();
        await LoadAturanAsync();
    }

    public AturanKuota SelectedAturan
    {
        get => _selAturan;
        set
        {
            if (!SetProperty(ref _selAturan, value) || value == null) return;
            _aturanId = value.AturanId;
            AturanBarang = BarangSubsidi.FirstOrDefault(b => b.BarangId == value.BarangId);
            AturanJenis = value.JenisPenerima;
            AturanPeriode = value.Periode;
            AturanJumlah = Num.Format(value.JumlahKuota);
        }
    }

    public Barang AturanBarang { get => _aturanBarang; set => SetProperty(ref _aturanBarang, value); }
    public string AturanJenis { get => _aturanJenis; set => SetProperty(ref _aturanJenis, value); }

    public string AturanPeriode
    {
        get => _aturanPeriode;
        set { if (SetProperty(ref _aturanPeriode, value)) Validate(nameof(AturanPeriode), MitraService.IsValidPeriode(value), "Format yyyy-MM, mis. 2026-10"); }
    }

    public string AturanJumlah
    {
        get => _aturanJumlah;
        set { if (SetProperty(ref _aturanJumlah, value)) Validate(nameof(AturanJumlah), value == "" || (Num.TryParse(value, out var j) && j > 0), "Jumlah harus > 0"); }
    }

    public string AturanFormJudul => _aturanId == 0 ? "Aturan kuota baru" : "Ubah aturan kuota";

    private async Task LoadAturanAsync()
    {
        if (BarangSubsidi.Count == 0)
            foreach (var b in await AppServices.Barang.ListAsync("SUBSIDI")) BarangSubsidi.Add(b);
        AturanList.Clear();
        foreach (var a in await AppServices.Mitra.ListAturanAsync(MitraService.IsValidPeriode(Periode) ? Periode : null)) AturanList.Add(a);
        await LoadSisaAsync();
    }

    private void NewAturan()
    {
        _aturanId = 0;
        _selAturan = null;
        OnPropertyChanged(nameof(SelectedAturan));
        AturanBarang = BarangSubsidi.FirstOrDefault();
        AturanPeriode = Periode;
        AturanJumlah = "";
        OnPropertyChanged(nameof(AturanFormJudul));
    }

    private async Task SaveAturanAsync()
    {
        if (!Num.TryParse(AturanJumlah, out var jumlah)) { ToastService.Warning("Isi jumlah kuota."); return; }
        await AppServices.Mitra.SaveAturanAsync(new AturanKuota
        {
            AturanId = _aturanId, BarangId = AturanBarang?.BarangId ?? 0, JenisPenerima = AturanJenis,
            Periode = AturanPeriode?.Trim(), JumlahKuota = jumlah
        });
        ToastService.Success("Aturan kuota tersimpan.");
        NewAturan();
        await LoadAturanAsync();
    }

    private async Task DeleteAturanAsync()
    {
        var a = SelectedAturan;
        if (!DialogService.Confirm($"Hapus aturan kuota {a.BarangNama} untuk {a.JenisLabel} periode {a.Periode}?", "Hapus aturan", "Hapus", true)) return;
        await AppServices.Mitra.DeleteAturanAsync(a);
        ToastService.Success("Aturan kuota dihapus.");
        NewAturan();
        await LoadAturanAsync();
    }

    private async Task CopyAturanAsync()
    {
        if (!MitraService.IsValidPeriode(Periode)) { ToastService.Warning("Isi periode tujuan dengan format yyyy-MM."); return; }
        var sebelumnya = Periode.Length == 7
            ? DateTime.ParseExact(Periode + "-01", "yyyy-MM-dd", null).AddMonths(-1).ToString("yyyy-MM")
            : (int.Parse(Periode) - 1).ToString();
        var dari = DialogService.Prompt($"Salin semua aturan kuota ke periode {Periode} dari periode:", "Salin aturan kuota", "Periode sumber", sebelumnya, false, "Salin");
        if (dari == null) return;
        var n = await AppServices.Mitra.CopyAturanAsync(dari.Trim(), Periode);
        ToastService.Success($"{n} aturan disalin ke periode {Periode}.");
        await LoadAturanAsync();
    }

    // ================= Pemasok =================
    public ObservableCollection<Pemasok> PemasokList { get; } = new();
    public string CariPemasok { get => _cariPemasok; set => SetProperty(ref _cariPemasok, value); }

    public Pemasok SelectedPemasok
    {
        get => _selPemasok;
        set
        {
            if (SetProperty(ref _selPemasok, value) && value != null)
                EditPemasok = new Pemasok
                {
                    PemasokId = value.PemasokId, Kode = value.Kode, Nama = value.Nama, Jenis = value.Jenis,
                    Telepon = value.Telepon, Alamat = value.Alamat, IsActive = value.IsActive
                };
            OnPropertyChanged(nameof(PemasokToggleLabel));
        }
    }

    public Pemasok EditPemasok { get => _editPemasok; set => SetProperty(ref _editPemasok, value); }
    public string PemasokToggleLabel => SelectedPemasok?.IsActive == false ? "Aktifkan" : "Nonaktifkan";

    private async Task SavePemasokAsync()
    {
        await AppServices.Mitra.SavePemasokAsync(EditPemasok);
        ToastService.Success($"Pemasok {EditPemasok.Nama} tersimpan.");
        await LoadPemasokAsync();
    }

    private async Task TogglePemasokAsync()
    {
        var p = SelectedPemasok;
        await AppServices.Mitra.SetPemasokActiveAsync(p, !p.IsActive);
        ToastService.Success($"{p.Nama} {(p.IsActive ? "dinonaktifkan" : "diaktifkan")}.");
        await LoadPemasokAsync();
    }

    private async Task LoadPemasokAsync()
    {
        var keep = SelectedPemasok?.PemasokId;
        PemasokList.Clear();
        foreach (var p in await AppServices.Mitra.ListPemasokAsync(CariPemasok)) PemasokList.Add(p);
        SelectedPemasok = PemasokList.FirstOrDefault(p => p.PemasokId == keep) ?? PemasokList.FirstOrDefault();
    }

    // ================= Penitip =================
    public ObservableCollection<Penitip> PenitipList { get; } = new();
    public string CariPenitip { get => _cariPenitip; set => SetProperty(ref _cariPenitip, value); }

    public Penitip SelectedPenitip
    {
        get => _selPenitip;
        set
        {
            if (SetProperty(ref _selPenitip, value) && value != null)
                EditPenitip = new Penitip
                {
                    PenitipId = value.PenitipId, Nama = value.Nama, Telepon = value.Telepon, Alamat = value.Alamat, IsActive = value.IsActive
                };
            OnPropertyChanged(nameof(PenitipToggleLabel));
        }
    }

    public Penitip EditPenitip { get => _editPenitip; set => SetProperty(ref _editPenitip, value); }
    public string PenitipToggleLabel => SelectedPenitip?.IsActive == false ? "Aktifkan" : "Nonaktifkan";

    private async Task SavePenitipAsync()
    {
        await AppServices.Mitra.SavePenitipAsync(EditPenitip);
        ToastService.Success($"Penitip {EditPenitip.Nama} tersimpan.");
        await LoadPenitipAsync();
    }

    private async Task TogglePenitipAsync()
    {
        var p = SelectedPenitip;
        await AppServices.Mitra.SetPenitipActiveAsync(p, !p.IsActive);
        ToastService.Success($"{p.Nama} {(p.IsActive ? "dinonaktifkan" : "diaktifkan")}.");
        await LoadPenitipAsync();
    }

    private async Task LoadPenitipAsync()
    {
        var keep = SelectedPenitip?.PenitipId;
        PenitipList.Clear();
        foreach (var p in await AppServices.Mitra.ListPenitipAsync(CariPenitip)) PenitipList.Add(p);
        SelectedPenitip = PenitipList.FirstOrDefault(p => p.PenitipId == keep) ?? PenitipList.FirstOrDefault();
    }

    // ================= Umum =================
    public override async Task LoadAsync()
    {
        // Pilih tab pertama yang boleh dilihat peran ini.
        _tab = ShowAnggota ? 0 : 2;
        OnPropertyChanged(nameof(Tab));
        await LoadTabAsync();
    }

    private async Task SafeLoadTab()
    {
        try { await LoadTabAsync(); } catch (Exception ex) { ErrorHandler.Handle(ex); }
    }

    private Task LoadTabAsync() => Tab switch
    {
        0 => LoadAnggotaAsync(SelectedAnggota?.NomorAnggota),
        1 => LoadPenerimaAsync(SelectedPenerima?.NIK),
        2 => LoadPemasokAsync(),
        3 => LoadPenitipAsync(),
        _ => Task.CompletedTask
    };

    public void OnNew()
    {
        switch (Tab)
        {
            case 0: NewAnggotaCommand.Execute(null); break;
            case 1: NewPenerimaCommand.Execute(null); break;
            case 2: NewPemasokCommand.Execute(null); break;
            case 3: NewPenitipCommand.Execute(null); break;
        }
    }

    public void OnSave()
    {
        switch (Tab)
        {
            case 0: SaveAnggotaCommand.Execute(null); break;
            case 1: SavePenerimaCommand.Execute(null); break;
            case 2: SavePemasokCommand.Execute(null); break;
            case 3: SavePenitipCommand.Execute(null); break;
        }
    }

    public void OnEscape() { }
}
