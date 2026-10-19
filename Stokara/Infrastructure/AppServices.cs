using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Stokara.Models;
using Stokara.Repositories;
using Stokara.Services;

namespace Stokara.Infrastructure;

/// <summary>
/// Composition root sederhana (dependency injection manual): repository dan service dibuat sekali
/// dan disuntikkan lewat konstruktor. ViewModel mengambil service dari sini.
/// </summary>
public static class AppServices
{
    public static AppConfig Config { get; private set; }
    public static Db Db { get; private set; }

    public static ActivityService Activity { get; private set; }
    public static SettingsService Settings { get; private set; }
    public static AuthService Auth { get; private set; }
    public static UserService Users { get; private set; }
    public static BarangService Barang { get; private set; }
    public static MitraService Mitra { get; private set; }
    public static StokService Stok { get; private set; }
    public static PenjualanService Penjualan { get; private set; }
    public static KonsinyasiService Konsinyasi { get; private set; }
    public static OpnameService Opname { get; private set; }
    public static DashboardService Dashboard { get; private set; }
    public static LaporanService Laporan { get; private set; }
    public static BackupService Backup { get; private set; }

    internal static UserRepository UserRepo { get; private set; }

    public static void Build(AppConfig config)
    {
        Config = config;
        Db = new Db(config.ConnectionString);

        var barangRepo = new BarangRepository(Db);
        var masterRepo = new MasterRepository(Db);
        var mitraRepo = new MitraRepository(Db);
        var kuotaRepo = new KuotaRepository(Db);
        var trxRepo = new TransaksiRepository(Db);
        var konsinyasiRepo = new KonsinyasiRepository(Db);
        var opnameRepo = new OpnameRepository(Db);
        var reportRepo = new ReportRepository(Db);
        var aktivitasRepo = new AktivitasRepository(Db);
        UserRepo = new UserRepository(Db);

        Activity = new ActivityService(aktivitasRepo);
        Settings = new SettingsService(masterRepo, Activity);
        Auth = new AuthService(UserRepo, Activity, config);
        Users = new UserService(UserRepo, Activity);
        Barang = new BarangService(barangRepo, Activity);
        Mitra = new MitraService(masterRepo, mitraRepo, kuotaRepo, Activity);
        Stok = new StokService(Db, barangRepo, trxRepo, aktivitasRepo);
        Penjualan = new PenjualanService(Db, barangRepo, mitraRepo, kuotaRepo, konsinyasiRepo, trxRepo, aktivitasRepo, Settings);
        Konsinyasi = new KonsinyasiService(Db, konsinyasiRepo, trxRepo, aktivitasRepo);
        Opname = new OpnameService(Db, opnameRepo, barangRepo, trxRepo, aktivitasRepo);
        Dashboard = new DashboardService(reportRepo, Activity, Settings);
        Laporan = new LaporanService(reportRepo, Settings, Activity);
        Backup = new BackupService(Db, Activity);
    }
}

/// <summary>
/// Menyiapkan database saat aplikasi pertama kali dijalankan: membuat database, menjalankan schema.sql dan seed.sql,
/// membuat akun awal, dan (opsional) data transaksi contoh.
/// </summary>
public static class DbInitializer
{
    public static readonly (string Username, string Password, string Nama, string Role)[] DefaultUsers =
    {
        ("manajer", "manajer123", "Manajer Gerai", Roles.Manajer),
        ("kasir", "kasir123", "Kasir Gerai", Roles.Kasir),
        ("gudang", "gudang123", "Petugas Gudang", Roles.Gudang),
        ("pengurus", "pengurus123", "Ketua Pengurus", Roles.Pengurus)
    };

    public static async Task EnsureAsync(AppConfig cfg, IProgress<string> progress)
    {
        var builder = new SqlConnectionStringBuilder(cfg.ConnectionString);
        var dbName = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(dbName) || !Regex.IsMatch(dbName, "^[A-Za-z0-9_]+$"))
            throw new InvalidOperationException("Nama database pada appsettings.json tidak valid (hanya huruf, angka, garis bawah).");

        progress?.Report("Menghubungkan ke SQL Server…");
        var master = new SqlConnectionStringBuilder(cfg.ConnectionString) { InitialCatalog = "master" }.ConnectionString;
        await using (var m = new SqlConnection(master))
        {
            await m.OpenAsync();
            await m.ExecAsync($"IF DB_ID(N'{dbName}') IS NULL CREATE DATABASE [{dbName}];");
        }

        await using var conn = new SqlConnection(cfg.ConnectionString);
        await conn.OpenAsync();
        var hasSchema = await conn.ScalarAsync<int>("SELECT CASE WHEN OBJECT_ID(N'dbo.Users', N'U') IS NULL THEN 0 ELSE 1 END") == 1;
        if (!hasSchema)
        {
            progress?.Report("Membuat tabel database…");
            await using var tx = conn.BeginTransaction();
            try
            {
                foreach (var file in new[] { "schema.sql", "seed.sql" })
                {
                    var path = Path.Combine(AppContext.BaseDirectory, "Database", file);
                    if (!File.Exists(path)) throw new FileNotFoundException($"Script {file} tidak ditemukan di folder Database.", path);
                    foreach (var batch in SplitBatches(await File.ReadAllTextAsync(path)))
                    {
                        await using var cmd = new SqlCommand(batch, conn, tx) { CommandTimeout = 120 };
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
            Logger.Info("Skema database STOKARA dibuat.");
        }

        if (await AppServices.UserRepo.CountAsync() == 0)
        {
            progress?.Report("Membuat akun awal…");
            foreach (var (u, p, n, r) in DefaultUsers)
                await AppServices.UserRepo.InsertAsync(new User { Username = u, Nama = n, Role = r, IsActive = true }, PasswordHasher.Hash(p));
        }

        await AppServices.Settings.RefreshAsync();

        if (cfg.SeedDemoData && AppServices.Settings.Get("DemoSeeded") != "1")
        {
            progress?.Report("Menyiapkan data contoh…");
            await DemoDataSeeder.RunAsync();
        }
    }

    private static IEnumerable<string> SplitBatches(string sql)
        => Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                .Where(s => !string.IsNullOrWhiteSpace(s));
}

/// <summary>
/// Data contoh dibuat lewat service (bukan INSERT langsung) agar mutasi, HPP, kuota, dan hutang penitip tetap konsisten.
/// Menyala lewat "SeedDemoData": true pada appsettings.json dan hanya berjalan sekali.
/// </summary>
public static class DemoDataSeeder
{
    public static async Task RunAsync()
    {
        var manajer = await AppServices.UserRepo.GetByUsernameAsync("manajer");
        if (manajer == null) return;
        Session.Start(manajer.User);
        try
        {
            var barang = await AppServices.Barang.ListAsync();
            Barang B(string sku) => barang.First(b => b.SKU == sku);
            LineItem L(string sku, decimal qty, decimal harga, int? expDays = null) => new()
            {
                Barang = B(sku), Jumlah = qty, Harga = harga,
                TanggalKedaluwarsa = expDays.HasValue ? DateTime.Today.AddDays(expDays.Value) : null
            };
            var pemasok = await AppServices.Mitra.ListPemasokAsync();
            int P(string kode) => pemasok.First(p => p.Kode == kode).PemasokId;

            var start = DateTime.Today.AddDays(-9).AddHours(8);

            await AppServices.Stok.SavePenerimaanAsync(new PenerimaanRequest
            {
                Sumber = "STOK_AWAL", Catatan = "Stok awal pembukaan gerai", Tanggal = start,
                Items =
                {
                    L("SBK-001", 30, 64000, 180), L("SBK-003", 48, 15500, 240), L("SBK-004", 40, 15000, 300),
                    L("SBK-005", 15, 25500, 14), L("MNM-001", 96, 2800, 365), L("RT-001", 12, 12500)
                }
            });
            await AppServices.Stok.SavePenerimaanAsync(new PenerimaanRequest
            {
                Sumber = "SUBSIDI", PemasokId = P("BULOG"), NomorSuratJalan = "SJ-BLG-0912", Tanggal = start.AddHours(1),
                Items = { L("SBK-002", 40, 58000, 120) }
            });
            await AppServices.Stok.SavePenerimaanAsync(new PenerimaanRequest
            {
                Sumber = "SUBSIDI", PemasokId = P("LPG"), NomorSuratJalan = "SJ-LPG-0331", Tanggal = start.AddHours(2),
                Items = { L("GAS-001", 60, 16000) }
            });
            await AppServices.Stok.SavePenerimaanAsync(new PenerimaanRequest
            {
                Sumber = "SUBSIDI", PemasokId = P("PUPUK"), NomorSuratJalan = "SJ-PPK-1107", Tanggal = start.AddHours(3),
                Items = { L("PPK-001", 30, 105000), L("PPK-002", 20, 107000) }
            });
            var penitip = await AppServices.Mitra.ListPenitipAsync();
            await AppServices.Stok.SavePenerimaanAsync(new PenerimaanRequest
            {
                Sumber = "KONSINYASI", PenitipId = penitip.First(p => p.Nama.Contains("Keripik")).PenitipId, Tanggal = start.AddHours(4),
                Items = { L("UMK-001", 30, 8000, 25) }
            });
            await AppServices.Stok.SavePenerimaanAsync(new PenerimaanRequest
            {
                Sumber = "KONSINYASI", PenitipId = penitip.First(p => p.Nama.Contains("Madu")).PenitipId, Tanggal = start.AddHours(5),
                Items = { L("UMK-002", 10, 45000, 400) }
            });

            // Penjualan 7 hari terakhir supaya dashboard dan laporan punya data.
            var rnd = new Random(42);
            var jualan = new[] { "SBK-001", "SBK-003", "SBK-004", "SBK-005", "MNM-001", "RT-001", "UMK-001", "UMK-002" };
            var anggota = await AppServices.Mitra.ListAnggotaAsync();
            for (var d = 7; d >= 1; d--)
            {
                var hari = DateTime.Today.AddDays(-d);
                var n = rnd.Next(3, 7);
                for (var i = 0; i < n; i++)
                {
                    var items = Enumerable.Range(0, rnd.Next(1, 4))
                        .Select(_ => (B(jualan[rnd.Next(jualan.Length)]).BarangId, (decimal)rnd.Next(1, 4)))
                        .GroupBy(x => x.Item1).Select(g => (g.Key, g.Sum(x => x.Item2))).ToList();
                    var req = new SaleRequest
                    {
                        Jenis = "PENJUALAN",
                        AnggotaId = rnd.Next(3) == 0 ? anggota[rnd.Next(anggota.Count)].AnggotaId : null,
                        Items = items,
                        UangDiterima = 1_000_000,
                        Tanggal = hari.AddHours(8 + i)
                    };
                    try { await AppServices.Penjualan.SaveAsync(req); }
                    catch (BusinessException) { /* stok habis: lewati */ }
                }
            }

            // Penyaluran subsidi bulan berjalan.
            var penerima = await AppServices.Mitra.ListPenerimaAsync();
            async Task Salur(string nama, string sku, decimal qty)
            {
                var p = penerima.FirstOrDefault(x => x.Nama == nama);
                if (p == null) return;
                try
                {
                    await AppServices.Penjualan.SaveAsync(new SaleRequest
                    {
                        Jenis = "PENYALURAN_SUBSIDI", PenerimaId = p.PenerimaId,
                        Items = { (B(sku).BarangId, qty) }, UangDiterima = 1_000_000,
                        Tanggal = DateTime.Today.Day > 1 ? DateTime.Today.AddHours(-2) : DateTime.Now
                    });
                }
                catch (BusinessException ex) { Logger.Info("Seeder: " + ex.Message); }
            }
            await Salur("Siti Aminah", "GAS-001", 2);
            await Salur("Siti Aminah", "SBK-002", 1);
            await Salur("Budi Santoso", "PPK-001", 2);
            await Salur("Sutrisno", "PPK-002", 1);
            await Salur("Warung Bu Yati", "GAS-001", 5);

            // Sebagian hutang penitip dibayar.
            var ringkas = await AppServices.Konsinyasi.RingkasanAsync();
            var keripik = ringkas.FirstOrDefault(r => r.Nama.Contains("Keripik"));
            if (keripik != null && keripik.Sisa > 0)
                await AppServices.Konsinyasi.BayarAsync(keripik.PenitipId, Math.Floor(keripik.Sisa / 2), "Pembayaran contoh");

            await AppServices.Settings.SaveAsync(new Dictionary<string, string> { ["DemoSeeded"] = "1" });
            Logger.Info("Data contoh dibuat.");
        }
        finally
        {
            Session.End();
        }
    }
}
