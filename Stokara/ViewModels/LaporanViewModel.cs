using System.Collections.ObjectModel;
using System.Data;
using System.Windows.Input;
using Stokara.Controls;
using Stokara.Helpers;
using Stokara.Infrastructure;
using Stokara.Models;
using Stokara.Services;

namespace Stokara.ViewModels;

/// <summary>Laporan dengan filter periode, grafik, tabel, cetak, dan ekspor (FR-27, FR-28).</summary>
public sealed class LaporanViewModel : ViewModelBase
{
    private KeyValuePair<string, string> _jenis;
    private DateTime _dari = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime _sampai = DateTime.Today;
    private LookupItem _kategori;
    private string _filterJenis = "";
    private string _status = "";
    private Barang _barang;
    private Penitip _penitip;
    private string _dusun = "";
    private ReportResult _result;

    public LaporanViewModel()
    {
        JenisLaporan = LaporanService.Jenis.Where(j => Session.Is(j.Roles))
            .Select(j => new KeyValuePair<string, string>(j.Kode, j.Nama)).ToList();
        _jenis = JenisLaporan.FirstOrDefault();
        GenerateCommand = new AsyncRelayCommand(GenerateAsync);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => Result != null);
        PrintCommand = new RelayCommand(Print, () => Result != null);
        PresetCommand = new AsyncRelayCommand(async p => { ApplyPreset(p as string); await GenerateAsync(); });
    }

    public ICommand GenerateCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand PresetCommand { get; }

    public List<KeyValuePair<string, string>> JenisLaporan { get; }

    public KeyValuePair<string, string> Jenis
    {
        get => _jenis;
        set
        {
            if (!SetProperty(ref _jenis, value)) return;
            _filterJenis = "";
            OnPropertyChanged(nameof(FilterJenis));
            OnPropertyChanged(nameof(FilterJenisOptions));
            foreach (var p in new[] { nameof(ShowPeriode), nameof(ShowKategori), nameof(ShowFilterJenis), nameof(ShowStatus),
                         nameof(ShowBarang), nameof(ShowPenitip), nameof(ShowDusun), nameof(IsRekap), nameof(Petunjuk) })
                OnPropertyChanged(p);
            Result = null;
        }
    }

    private string Kode => Jenis.Key;
    public bool ShowPeriode => Kode != "STOK";
    public bool IsRekap => Kode == "REKAP";
    public bool ShowKategori => Kode is "PENJUALAN" or "STOK";
    public bool ShowFilterJenis => Kode is "PENJUALAN" or "STOK" or "MUTASI";
    public bool ShowStatus => Kode == "STOK";
    public bool ShowBarang => Kode is "SUBSIDI" or "MUTASI";
    public bool ShowPenitip => Kode == "KONSINYASI";
    public bool ShowDusun => Kode == "SUBSIDI";

    public string Petunjuk => Kode switch
    {
        "PENJUALAN" => "Penjualan per barang beserta HPP/hak penitip dan laba kotor (bukan pengganti akuntansi koperasi).",
        "STOK" => "Posisi stok saat ini, status, nilai persediaan milik koperasi, dan perkiraan kedaluwarsa.",
        "SUBSIDI" => "Barang subsidi yang tersalur per penerima, per barang, per dusun.",
        "KONSINYASI" => "Penjualan barang titipan, hak penitip, komisi koperasi, dan sisa hutang.",
        "MUTASI" => "Seluruh pergerakan stok dalam periode.",
        "REKAP" => "Ringkasan satu bulan untuk pengurus. Pilih tanggal mana pun pada bulan yang diinginkan.",
        "HARIAN" => "Rekap penjualan per barang untuk tanggal yang dipilih.",
        _ => null
    };

    public List<KeyValuePair<string, string>> FilterJenisOptions => Kode switch
    {
        "PENJUALAN" => new() { new("", "Semua"), new("UMUM", "Pelanggan umum"), new("ANGGOTA", "Anggota"), new("SUBSIDI", "Penyaluran subsidi") },
        "STOK" => new() { new("", "Semua"), new("BEBAS", "Barang bebas"), new("SUBSIDI", "Subsidi"), new("TITIPAN", "Titipan") },
        "MUTASI" => new List<KeyValuePair<string, string>> { new("", "Semua jenis") }
            .Concat(new[] { "MASUK_STOK_AWAL", "MASUK_PEMBELIAN", "MASUK_SUBSIDI", "MASUK_KONSINYASI", "KELUAR_JUAL", "KELUAR_SUBSIDI",
                            "KELUAR_RETUR", "KELUAR_RUSAK", "VOID", "PENYESUAIAN" }
                .Select(k => new KeyValuePair<string, string>(k, Fmt.MutasiLabel(k)))).ToList(),
        _ => new()
    };

    public List<KeyValuePair<string, string>> StatusOptions { get; } = new()
    {
        new("", "Semua status"), new("Aman", "Aman"), new("Menipis", "Menipis"), new("Habis", "Habis")
    };

    public DateTime Dari { get => _dari; set => SetProperty(ref _dari, value); }
    public DateTime Sampai { get => _sampai; set => SetProperty(ref _sampai, value); }
    public LookupItem Kategori { get => _kategori; set => SetProperty(ref _kategori, value); }
    public string FilterJenis { get => _filterJenis; set => SetProperty(ref _filterJenis, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public Barang Barang { get => _barang; set => SetProperty(ref _barang, value); }
    public Penitip Penitip { get => _penitip; set => SetProperty(ref _penitip, value); }
    public string Dusun { get => _dusun; set => SetProperty(ref _dusun, value); }

    public ObservableCollection<LookupItem> KategoriList { get; } = new();
    public ObservableCollection<Barang> BarangList { get; } = new();
    public ObservableCollection<Penitip> PenitipList { get; } = new();
    public ObservableCollection<string> DusunList { get; } = new();

    public ReportResult Result
    {
        get => _result;
        private set
        {
            SetProperty(ref _result, value);
            OnPropertyChanged(nameof(Table));
            OnPropertyChanged(nameof(ChartPoints));
            OnPropertyChanged(nameof(ChartKind));
            OnPropertyChanged(nameof(HasResult));
            OnPropertyChanged(nameof(RowCount));
        }
    }

    public bool HasResult => Result != null;
    public DataView Table => Result?.Table?.DefaultView;
    public ObservableCollection<ChartPoint> ChartPoints => Result == null ? null : new ObservableCollection<ChartPoint>(Result.Chart);
    public ChartKind ChartKind => Result?.ChartJenis switch { "Line" => ChartKind.Line, "Progress" => ChartKind.Progress, _ => ChartKind.Bar };
    public string RowCount => Result == null ? "" : $"{Result.Table.Rows.Count:N0} baris";

    public override async Task LoadAsync()
    {
        KategoriList.Add(new LookupItem { Id = 0, Nama = "Semua kategori" });
        foreach (var k in await AppServices.Mitra.ListLookupAsync("Kategori")) KategoriList.Add(k);
        Kategori = KategoriList[0];
        BarangList.Add(new Barang { BarangId = 0, Nama = "Semua barang", SKU = "-" });
        foreach (var b in await AppServices.Barang.ListAsync()) BarangList.Add(b);
        Barang = BarangList[0];
        PenitipList.Add(new Penitip { PenitipId = 0, Nama = "Semua penitip" });
        foreach (var p in await AppServices.Mitra.ListPenitipAsync()) PenitipList.Add(p);
        Penitip = PenitipList[0];
        DusunList.Add("");
        foreach (var d in await AppServices.Laporan.ListDusunAsync()) DusunList.Add(d);
        if (Session.Is(Roles.Kasir))
        {
            Dari = Sampai = DateTime.Today;
        }
        if (JenisLaporan.Count > 0) await GenerateAsync();
    }

    private void ApplyPreset(string preset)
    {
        var today = DateTime.Today;
        switch (preset)
        {
            case "HARI": Dari = Sampai = today; break;
            case "7": Dari = today.AddDays(-6); Sampai = today; break;
            case "BULAN": Dari = new DateTime(today.Year, today.Month, 1); Sampai = today; break;
            case "BULANLALU":
                Dari = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                Sampai = new DateTime(today.Year, today.Month, 1).AddDays(-1);
                break;
        }
    }

    private async Task GenerateAsync()
    {
        if (Jenis.Key == null) return;
        if (Session.Is(Roles.Kasir) && Kode == "HARIAN" && (Dari.Date != DateTime.Today || Sampai.Date != DateTime.Today))
        {
            Dari = Sampai = DateTime.Today;
            ToastService.Info("Kasir hanya dapat melihat rekap harian hari ini.");
        }
        IsBusy = true;
        try
        {
            Result = await AppServices.Laporan.GenerateAsync(Kode, Dari, Sampai,
                Kategori == null || Kategori.Id == 0 ? null : Kategori.Id,
                string.IsNullOrEmpty(FilterJenis) ? null : FilterJenis,
                string.IsNullOrEmpty(Status) ? null : Status,
                Barang == null || Barang.BarangId == 0 ? null : Barang.BarangId,
                Penitip == null || Penitip.PenitipId == 0 ? null : Penitip.PenitipId,
                string.IsNullOrEmpty(Dusun) ? null : Dusun);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExportAsync()
    {
        var name = $"{Result.Judul.Replace(' ', '_').Replace('&', 'n')}_{DateTime.Now:yyyyMMdd_HHmm}.csv";
        var path = DialogService.SaveFile("CSV untuk Excel (*.csv)|*.csv", name);
        if (path == null) return;
        ExportService.ToCsv(Result.Table, path, Result.Judul, Result.Subjudul);
        await AppServices.Laporan.LogExportAsync(Result.Judul, path);
        ToastService.Success("Laporan diekspor. File dapat dibuka dengan Excel.");
    }

    private void Print()
    {
        PrintService.Print(PrintService.BuildReport(Result), Result.Judul);
    }
}
