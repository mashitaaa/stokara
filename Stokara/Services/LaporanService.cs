using System.Data;
using Stokara.Infrastructure;
using Stokara.Models;
using Stokara.Repositories;

namespace Stokara.Services;

public sealed class DashboardData
{
    public DashboardStats Stats { get; set; }
    public List<ChartPoint> Penjualan { get; set; } = new();
    public List<ChartPoint> Terlaris { get; set; } = new();
    public List<ChartPoint> Kuota { get; set; } = new();
    public List<PeringatanRow> StokKritis { get; set; } = new();
    public List<PeringatanRow> Kedaluwarsa { get; set; } = new();
    public List<Aktivitas> Aktivitas { get; set; } = new();
}

public sealed class DashboardService
{
    private readonly ReportRepository _repo;
    private readonly ActivityService _activity;
    private readonly SettingsService _settings;

    public DashboardService(ReportRepository repo, ActivityService activity, SettingsService settings)
    {
        _repo = repo;
        _activity = activity;
        _settings = settings;
    }

    /// <summary>
    /// Data dashboard sesuai peran (matriks 4.1): Kasir melihat penjualannya sendiri dan stok kritis;
    /// Petugas Gudang melihat stok kritis, perkiraan kedaluwarsa, dan aktivitas.
    /// </summary>
    public async Task<DashboardData> LoadAsync(int hariGrafik)
    {
        var kasirOnly = Session.Is(Roles.Kasir) ? Session.UserId : (int?)null;
        var periode = _settings.PeriodeSekarang;
        var ambang = _settings.AmbangKedaluwarsaHari;
        var data = new DashboardData
        {
            Stats = await _repo.StatsAsync(kasirOnly, periode, ambang),
            StokKritis = await _repo.StokKritisAsync(15)
        };
        data.Kedaluwarsa = await _repo.PerkiraanKedaluwarsaAsync(ambang, 15);
        data.Stats.PerkiraanKedaluwarsa = data.Kedaluwarsa.Count;
        data.Penjualan = await _repo.PenjualanHarianAsync(hariGrafik, kasirOnly);

        if (!Session.Is(Roles.Kasir))
        {
            data.Terlaris = await _repo.TerlarisAsync(30, 6);
            data.Kuota = await _repo.KuotaPerBarangAsync(periode);
            data.Aktivitas = await _activity.RecentAsync(8);
        }
        return data;
    }
}

public sealed class ReportResult
{
    public string Judul { get; set; }
    public string Subjudul { get; set; }
    public DataTable Table { get; set; }
    public List<ChartPoint> Chart { get; set; } = new();
    public string ChartJudul { get; set; }
    /// <summary>"Bar", "Line", atau "Progress".</summary>
    public string ChartJenis { get; set; } = "Bar";
    public List<KeyValuePair<string, string>> Ringkasan { get; set; } = new();
}

/// <summary>Enam laporan PRD v1.1 bagian 13 dan ekspor (FR-27, FR-28).</summary>
public sealed class LaporanService
{
    private readonly ReportRepository _repo;
    private readonly SettingsService _settings;
    private readonly ActivityService _log;

    public LaporanService(ReportRepository repo, SettingsService settings, ActivityService log)
    {
        _repo = repo;
        _settings = settings;
        _log = log;
    }

    public static readonly (string Kode, string Nama, string[] Roles)[] Jenis =
    {
        ("PENJUALAN", "Penjualan & Laba Kotor", new[] { Roles.Manajer, Roles.Pengurus }),
        ("STOK", "Stok & Nilai Persediaan", new[] { Roles.Manajer, Roles.Pengurus, Roles.Gudang }),
        ("SUBSIDI", "Penyaluran Subsidi", new[] { Roles.Manajer, Roles.Pengurus }),
        ("KONSINYASI", "Konsinyasi", new[] { Roles.Manajer, Roles.Pengurus }),
        ("MUTASI", "Mutasi Stok", new[] { Roles.Manajer, Roles.Pengurus, Roles.Gudang }),
        ("REKAP", "Rekap Bulanan", new[] { Roles.Manajer, Roles.Pengurus }),
        ("HARIAN", "Rekap Harian Kasir", new[] { Roles.Manajer, Roles.Pengurus, Roles.Kasir })
    };

    public Task<List<string>> ListDusunAsync() => _repo.ListDusunAsync();

    public async Task<ReportResult> GenerateAsync(string kode, DateTime dari, DateTime sampai, int? kategoriId, string jenis,
        string status, int? barangId, int? penitipId, string dusun)
    {
        var def = Jenis.FirstOrDefault(j => j.Kode == kode);
        if (def.Kode == null) throw new BusinessException("Pilih jenis laporan.");
        Session.Require($"melihat laporan {def.Nama}", def.Roles);
        if (sampai.Date < dari.Date) throw new BusinessException("Tanggal akhir tidak boleh sebelum tanggal awal.");
        var periodeTeks = $"{Fmt.Tgl(dari)} s.d. {Fmt.Tgl(sampai)}";

        ReportResult result;
        switch (kode)
        {
            case "PENJUALAN":
            {
                var t = await _repo.PenjualanPerBarangAsync(dari, sampai, jenis, kategoriId);
                result = new ReportResult
                {
                    Judul = "Laporan Penjualan & Laba Kotor", Subjudul = periodeTeks, Table = t,
                    Chart = await _repo.PenjualanPerHariAsync(dari, sampai), ChartJudul = "Penjualan per hari (Rp)", ChartJenis = "Line"
                };
                result.Ringkasan.Add(new("Total penjualan", Fmt.Rp(Sum(t, "Penjualan Rp"))));
                result.Ringkasan.Add(new("HPP & hak penitip", Fmt.Rp(Sum(t, "HPP atau Hak Penitip Rp"))));
                result.Ringkasan.Add(new("Laba kotor", Fmt.Rp(Sum(t, "Laba Kotor Rp"))));
                break;
            }
            case "STOK":
            {
                var t = await _repo.StokAsync(kategoriId, jenis, status, _settings.AmbangKedaluwarsaHari);
                result = new ReportResult
                {
                    Judul = "Laporan Stok & Nilai Persediaan", Subjudul = "Posisi " + Fmt.TglJam(DateTime.Now), Table = t,
                    ChartJudul = "Nilai persediaan per kategori (Rp)", ChartJenis = "Bar",
                    Chart = t.AsEnumerable().GroupBy(r => r.Field<string>("Kategori"))
                        .Select(g => new ChartPoint(g.Key, (double)g.Sum(r => r.Field<decimal>("Nilai Persediaan Rp")))).ToList()
                };
                result.Ringkasan.Add(new("Jumlah barang", t.Rows.Count.ToString("N0", Fmt.Id)));
                result.Ringkasan.Add(new("Nilai persediaan milik koperasi", Fmt.Rp(Sum(t, "Nilai Persediaan Rp"))));
                result.Ringkasan.Add(new("Menipis / Habis",
                    $"{t.AsEnumerable().Count(r => r.Field<string>("Status") == "Menipis")} / {t.AsEnumerable().Count(r => r.Field<string>("Status") == "Habis")}"));
                break;
            }
            case "SUBSIDI":
            {
                var t = await _repo.SubsidiAsync(dari, sampai, barangId, dusun);
                var periode = _settings.PeriodeKuota(sampai);
                result = new ReportResult
                {
                    Judul = "Laporan Penyaluran Subsidi", Subjudul = periodeTeks, Table = t,
                    Chart = await _repo.KuotaPerBarangAsync(periode), ChartJudul = $"Kuota terpakai vs tersedia, periode {periode}",
                    ChartJenis = "Progress"
                };
                result.Ringkasan.Add(new("Penerima terlayani", t.AsEnumerable().Select(r => r.Field<string>("Penerima")).Distinct().Count().ToString()));
                result.Ringkasan.Add(new("Nilai tersalur", Fmt.Rp(Sum(t, "Nilai Rp"))));
                break;
            }
            case "KONSINYASI":
            {
                var t = await _repo.KonsinyasiAsync(dari, sampai, penitipId);
                result = new ReportResult
                {
                    Judul = "Laporan Konsinyasi", Subjudul = periodeTeks, Table = t, ChartJudul = "Sisa hutang per penitip (Rp)",
                    Chart = t.AsEnumerable().GroupBy(r => r.Field<string>("Penitip"))
                        .Select(g => new ChartPoint(g.Key, (double)g.Sum(r => r.Field<decimal>("Sisa Hutang Semua Periode Rp")))).ToList()
                };
                result.Ringkasan.Add(new("Hak penitip periode ini", Fmt.Rp(Sum(t, "Hak Penitip Rp"))));
                result.Ringkasan.Add(new("Komisi koperasi", Fmt.Rp(Sum(t, "Komisi Koperasi Rp"))));
                result.Ringkasan.Add(new("Sisa hutang (semua periode)", Fmt.Rp(Sum(t, "Sisa Hutang Semua Periode Rp"))));
                break;
            }
            case "MUTASI":
            {
                var t = await _repo.MutasiAsync(dari, sampai, barangId, string.IsNullOrEmpty(jenis) ? null : jenis);
                var col = t.Columns["Jenis Mutasi"];
                col.ReadOnly = false;
                col.MaxLength = -1;
                foreach (DataRow r in t.Rows) r[col] = Fmt.MutasiLabel(r.Field<string>(col));
                result = new ReportResult
                {
                    Judul = "Laporan Mutasi Stok", Subjudul = periodeTeks, Table = t, ChartJudul = "Jumlah mutasi per jenis",
                    Chart = t.AsEnumerable().GroupBy(r => r.Field<string>("Jenis Mutasi"))
                        .Select(g => new ChartPoint(g.Key, g.Count())).ToList()
                };
                result.Ringkasan.Add(new("Baris mutasi", t.Rows.Count.ToString("N0", Fmt.Id)));
                result.Ringkasan.Add(new("Total masuk", Fmt.Num(Sum(t, "Masuk"))));
                result.Ringkasan.Add(new("Total keluar", Fmt.Num(Sum(t, "Keluar"))));
                break;
            }
            case "REKAP":
                result = await RekapBulananAsync(dari);
                break;
            case "HARIAN":
            {
                var t = await _repo.PenjualanPerBarangAsync(dari, sampai, null, null);
                result = new ReportResult
                {
                    Judul = "Rekap Harian Kasir", Subjudul = periodeTeks, Table = t,
                    Chart = await _repo.PenjualanPerHariAsync(dari, sampai), ChartJudul = "Penjualan per hari (Rp)", ChartJenis = "Bar"
                };
                result.Ringkasan.Add(new("Total penjualan", Fmt.Rp(Sum(t, "Penjualan Rp"))));
                break;
            }
            default:
                throw new BusinessException("Jenis laporan tidak dikenal.");
        }
        return result;
    }

    private async Task<ReportResult> RekapBulananAsync(DateTime bulan)
    {
        var dari = new DateTime(bulan.Year, bulan.Month, 1);
        var sampai = dari.AddMonths(1).AddDays(-1);
        var periode = _settings.PeriodeKuota(dari);
        var v = await _repo.RekapAsync(dari, sampai, periode);

        var t = new DataTable();
        t.Columns.Add("Kelompok", typeof(string));
        t.Columns.Add("Uraian", typeof(string));
        t.Columns.Add("Nilai", typeof(string));
        void Row(string k, string u, string n) => t.Rows.Add(k, u, n);

        Row("Penjualan", "Jumlah transaksi penjualan", Fmt.Num(v["TrxJual"]));
        Row("Penjualan", "Nilai penjualan", Fmt.Rp(v["NilaiJual"]));
        Row("Penjualan", "Laba kotor (penjualan − HPP − hak penitip)", Fmt.Rp(v["LabaKotor"]));
        Row("Penjualan", "Transaksi dibatalkan", Fmt.Num(v["TrxBatal"]));
        Row("Subsidi", "Jumlah penyaluran", Fmt.Num(v["TrxSubsidi"]));
        Row("Subsidi", "Nilai penyaluran", Fmt.Rp(v["NilaiSubsidi"]));
        Row("Subsidi", $"Total kuota terpakai periode {periode} (unit)", Fmt.Num(v["KuotaTerpakai"]));
        Row("Konsinyasi", "Hak penitip dari penjualan bulan ini", Fmt.Rp(v["HutangBaru"]));
        Row("Konsinyasi", "Komisi koperasi", Fmt.Rp(v["Komisi"]));
        Row("Konsinyasi", "Pembayaran ke penitip", Fmt.Rp(v["Pelunasan"]));
        Row("Konsinyasi", "Sisa hutang penitip (saat ini)", Fmt.Rp(v["SisaHutang"]));
        Row("Stok", "Jumlah dokumen penerimaan", Fmt.Num(v["TrxTerima"]));
        Row("Stok", "Nilai pembelian & pasokan subsidi", Fmt.Rp(v["Pembelian"]));
        Row("Stok", "Dokumen retur/rusak/hilang", Fmt.Num(v["TrxKeluarLain"]));
        Row("Stok", "Sesi opname disetujui", Fmt.Num(v["OpnameDisetujui"]));
        Row("Stok", "Total unit penyesuaian opname", Fmt.Num(v["UnitPenyesuaian"]));
        Row("Stok", "Nilai persediaan milik koperasi (saat ini)", Fmt.Rp(v["NilaiPersediaan"]));
        Row("Kas", "Hari kasir ditutup", Fmt.Num(v["HariTutup"]));
        Row("Kas", "Total selisih kas tutup kasir", Fmt.Rp(v["SelisihKas"]));

        var result = new ReportResult
        {
            Judul = "Rekap Bulanan " + dari.ToString("MMMM yyyy", Fmt.Id),
            Subjudul = $"{_settings.NamaGerai} · {Fmt.Tgl(dari)} s.d. {Fmt.Tgl(sampai)}",
            Table = t,
            Chart = await _repo.PenjualanPerHariAsync(dari, sampai),
            ChartJudul = "Penjualan harian (Rp)",
            ChartJenis = "Bar"
        };
        result.Ringkasan.Add(new("Penjualan", Fmt.Rp(v["NilaiJual"])));
        result.Ringkasan.Add(new("Penyaluran subsidi", Fmt.Rp(v["NilaiSubsidi"])));
        result.Ringkasan.Add(new("Laba kotor", Fmt.Rp(v["LabaKotor"])));
        return result;
    }

    private static decimal Sum(DataTable t, string col)
        => t.Columns.Contains(col) ? t.AsEnumerable().Sum(r => r.IsNull(col) ? 0m : Convert.ToDecimal(r[col])) : 0m;

    public async Task LogExportAsync(string judul, string file)
        => await _log.LogAsync("EKSPOR", "Laporan", null, $"{judul} → {Path.GetFileName(file)}");
}
