using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Stokara.Helpers;
using Stokara.Infrastructure;
using Stokara.Models;

namespace Stokara.ViewModels;

/// <summary>Stok opname: Draft → Diajukan → Disetujui/Ditolak (FR-16, BR-15).</summary>
public sealed class OpnameViewModel : ViewModelBase, IShortcutTarget
{
    private OpnameRow _selected;
    private LookupItem _kategoriBaru;
    private string _catatanBaru = "";
    private bool _hanyaSelisih;

    public OpnameViewModel()
    {
        CreateCommand = new AsyncRelayCommand(CreateAsync, () => CanInput);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => IsDraft && CanInput);
        SubmitCommand = new AsyncRelayCommand(SubmitAsync, () => IsDraft && CanInput);
        ApproveCommand = new AsyncRelayCommand(ApproveAsync, () => CanDecide);
        RejectCommand = new AsyncRelayCommand(RejectAsync, () => CanDecide);
        SamakanCommand = new RelayCommand(() =>
        {
            foreach (var d in Detail.Where(x => !x.StokFisik.HasValue)) d.StokFisik = d.StokSistem;
            RaiseSummary();
        }, () => IsDraft && CanInput);
    }

    public ICommand CreateCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SubmitCommand { get; }
    public ICommand ApproveCommand { get; }
    public ICommand RejectCommand { get; }
    public ICommand SamakanCommand { get; }

    public bool CanInput => Session.Is(Roles.Manajer, Roles.Gudang);
    public bool CanApproveRole => Session.Is(Roles.Manajer, Roles.Pengurus);

    public ObservableCollection<OpnameRow> Sesi { get; } = new();
    public ObservableCollection<OpnameDetailRow> Detail { get; } = new();
    public ObservableCollection<LookupItem> KategoriList { get; } = new();

    public LookupItem KategoriBaru { get => _kategoriBaru; set => SetProperty(ref _kategoriBaru, value); }
    public string CatatanBaru { get => _catatanBaru; set => SetProperty(ref _catatanBaru, value); }

    public OpnameRow Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value)) return;
            OnPropertyChanged(nameof(IsDraft));
            OnPropertyChanged(nameof(CanDecide));
            OnPropertyChanged(nameof(StatusInfo));
            _ = LoadDetailAsync();
        }
    }

    public bool IsDraft => Selected?.Status == "DRAFT";

    /// <summary>Penyetuju harus berbeda dari pengaju (BR-15).</summary>
    public bool CanDecide => Selected?.Status == "DIAJUKAN" && CanApproveRole && Selected.DiajukanOlehId != Session.Current?.UserId;

    public string StatusInfo => Selected == null ? null : Selected.Status switch
    {
        "DRAFT" => "Isi stok fisik hasil hitung, beri alasan untuk setiap selisih, lalu ajukan.",
        "DIAJUKAN" => Selected.DiajukanOlehId == Session.Current?.UserId
            ? "Menunggu persetujuan Manajer/Pengurus lain. Anda tidak dapat menyetujui pengajuan sendiri."
            : $"Diajukan oleh {Selected.DiajukanOleh}. Periksa selisih, lalu setujui atau tolak.",
        "DISETUJUI" => $"Disetujui oleh {Selected.DisetujuiOleh}. Stok sudah disesuaikan.",
        "DITOLAK" => $"Ditolak oleh {Selected.DisetujuiOleh}: {Selected.AlasanTolak}",
        _ => null
    };

    public bool HanyaSelisih
    {
        get => _hanyaSelisih;
        set
        {
            if (SetProperty(ref _hanyaSelisih, value))
                System.Windows.Data.CollectionViewSource.GetDefaultView(Detail).Filter =
                    value ? new Predicate<object>(o => o is OpnameDetailRow r && r.Selisih.HasValue && r.Selisih != 0) : null;
        }
    }

    public int JumlahDihitung => Detail.Count(d => d.StokFisik.HasValue);
    public int JumlahSelisih => Detail.Count(d => d.Selisih.HasValue && d.Selisih != 0);
    public string Progress => Detail.Count == 0 ? "" : $"{JumlahDihitung} dari {Detail.Count} barang dihitung · {JumlahSelisih} berselisih";
    public double ProgressPersen => Detail.Count == 0 ? 0 : JumlahDihitung * 100.0 / Detail.Count;

    private void RaiseSummary()
    {
        OnPropertyChanged(nameof(JumlahDihitung));
        OnPropertyChanged(nameof(JumlahSelisih));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(ProgressPersen));
    }

    public override async Task LoadAsync()
    {
        if (KategoriList.Count == 0)
        {
            KategoriList.Add(new LookupItem { Id = 0, Nama = "Semua kategori" });
            foreach (var k in await AppServices.Mitra.ListLookupAsync("Kategori")) KategoriList.Add(k);
            KategoriBaru = KategoriList[0];
        }
        var keep = Selected?.OpnameId;
        Sesi.Clear();
        foreach (var s in await AppServices.Opname.ListAsync()) Sesi.Add(s);
        Selected = Sesi.FirstOrDefault(s => s.OpnameId == keep) ?? Sesi.FirstOrDefault();
    }

    private async Task LoadDetailAsync()
    {
        foreach (var d in Detail) d.PropertyChanged -= OnDetailChanged;
        Detail.Clear();
        if (Selected == null) { RaiseSummary(); return; }
        try
        {
            foreach (var d in await AppServices.Opname.DetailAsync(Selected.OpnameId))
            {
                d.PropertyChanged += OnDetailChanged;
                Detail.Add(d);
            }
        }
        catch (Exception ex) { ErrorHandler.Handle(ex); }
        RaiseSummary();
    }

    private void OnDetailChanged(object sender, PropertyChangedEventArgs e) => RaiseSummary();

    private async Task CreateAsync()
    {
        var kategori = KategoriBaru == null || KategoriBaru.Id == 0 ? (int?)null : KategoriBaru.Id;
        if (!DialogService.Confirm($"Buat sesi opname untuk {KategoriBaru?.Nama ?? "semua kategori"}? Stok sistem saat ini akan disalin sebagai pembanding.",
                "Sesi opname baru", "Buat sesi")) return;
        var id = await AppServices.Opname.CreateAsync(kategori, CatatanBaru);
        CatatanBaru = "";
        ToastService.Success("Sesi opname dibuat. Silakan isi hasil hitung fisik.");
        await LoadAsync();
        Selected = Sesi.FirstOrDefault(s => s.OpnameId == id);
    }

    private async Task SaveAsync()
    {
        await AppServices.Opname.SaveCountsAsync(Selected.OpnameId, Detail);
        ToastService.Success("Hasil hitung disimpan sebagai draft.");
    }

    private async Task SubmitAsync()
    {
        if (!DialogService.Confirm($"Ajukan penyesuaian untuk {JumlahSelisih} barang berselisih? Setelah diajukan, angka tidak dapat diubah.",
                "Ajukan opname", "Ajukan")) return;
        await AppServices.Opname.SubmitAsync(Selected.OpnameId, Detail);
        ToastService.Success("Opname diajukan untuk persetujuan.");
        await LoadAsync();
    }

    private async Task ApproveAsync()
    {
        if (!DialogService.Confirm($"Setujui penyesuaian {JumlahSelisih} barang? Stok akan diubah sesuai selisih dan tercatat sebagai mutasi PENYESUAIAN.",
                "Setujui opname", "Setujui")) return;
        await AppServices.Opname.ApproveAsync(Selected.OpnameId);
        ToastService.Success("Penyesuaian disetujui. Stok sudah diperbarui.");
        await LoadAsync();
    }

    private async Task RejectAsync()
    {
        var alasan = DialogService.Prompt("Opname akan ditolak dan stok tidak diubah.", "Tolak opname", "Alasan penolakan", "", true, "Tolak");
        if (alasan == null) return;
        await AppServices.Opname.RejectAsync(Selected.OpnameId, alasan);
        ToastService.Info("Opname ditolak.");
        await LoadAsync();
    }

    public void OnNew() => CreateCommand.Execute(null);
    public void OnSave() { if (IsDraft) SaveCommand.Execute(null); }
    public void OnEscape() => HanyaSelisih = false;
}
