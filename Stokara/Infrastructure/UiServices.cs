using System.Collections.ObjectModel;
using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;

namespace Stokara.Infrastructure;

public sealed class Toast
{
    public string Message { get; init; }
    /// <summary>Success, Info, Warning, Error</summary>
    public string Kind { get; init; }

    public string Icon => Kind switch
    {
        "Success" => "\uE73E",
        "Warning" => "\uE7BA",
        "Error" => "\uEA39",
        _ => "\uE946"
    };

    public string Title => Kind switch
    {
        "Success" => "Berhasil",
        "Warning" => "Perhatian",
        "Error" => "Gagal",
        _ => "Info"
    };
}

/// <summary>Notifikasi toast yang muncul di pojok kanan bawah dan hilang sendiri (FR-30).</summary>
public static class ToastService
{
    public static ObservableCollection<Toast> Items { get; } = new();

    public static void Success(string m) => Show(m, "Success");
    public static void Info(string m) => Show(m, "Info");
    public static void Warning(string m) => Show(m, "Warning", 6);
    public static void Error(string m) => Show(m, "Error", 7);

    public static void Show(string message, string kind = "Info", int seconds = 4)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null) return;
        dispatcher.InvokeAsync(() =>
        {
            var toast = new Toast { Message = message, Kind = kind };
            Items.Add(toast);
            while (Items.Count > 4) Items.RemoveAt(0);
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Items.Remove(toast);
            };
            timer.Start();
        });
    }

    public static void Dismiss(Toast t) => Items.Remove(t);
}

/// <summary>Status koneksi database untuk indikator di header (FR-31).</summary>
public static class DbStatus
{
    private static bool _online = true;
    public static bool Online => _online;
    public static event Action<bool> Changed;

    public static void Set(bool online)
    {
        if (_online == online) return;
        _online = online;
        Application.Current?.Dispatcher.InvokeAsync(() => Changed?.Invoke(online));
    }
}

/// <summary>Penanganan error terpusat: pesan ramah untuk pengguna, detail teknis ke file log (FR-31, NFR-07).</summary>
public static class ErrorHandler
{
    private static readonly int[] ConnectionErrors = { -2, -1, 2, 53, 233, 258, 1225, 4060, 10053, 10054, 10060, 10061, 11001, 18456, 40613 };

    public static void Handle(Exception ex)
    {
        while (ex is AggregateException { InnerException: not null } agg) ex = agg.InnerException;
        if (ex is System.Reflection.TargetInvocationException { InnerException: not null } tie) ex = tie.InnerException;

        switch (ex)
        {
            case BusinessException be:
                ToastService.Warning(be.Message);
                break;
            case AccessDeniedException ad:
                ToastService.Error(ad.Message);
                _ = AppServices.Activity?.LogAsync("AKSES_DITOLAK", "Otorisasi", null, ad.Action);
                break;
            case SqlException se:
                Logger.Error("SQL", se);
                if (IsConnectionError(se)) DbStatus.Set(false);
                ToastService.Error(Friendly(se));
                break;
            case UnauthorizedAccessException or IOException:
                Logger.Error("File", ex);
                ToastService.Error("File tidak dapat dibuka atau disimpan: " + ex.Message);
                break;
            default:
                Logger.Error("Tak terduga", ex);
                ToastService.Error("Terjadi kesalahan tak terduga. Detail tersimpan di log aplikasi.");
                break;
        }
    }

    public static bool IsConnectionError(SqlException se) => se.Errors.Cast<SqlError>().Any(e => ConnectionErrors.Contains(e.Number));

    public static string Friendly(SqlException se)
    {
        if (IsConnectionError(se))
            return "Database tidak dapat dihubungi. Pastikan layanan SQL Server (SQLEXPRESS) berjalan, lalu coba lagi.";
        return se.Number switch
        {
            2627 or 2601 => "Data dengan kode/NIK/nomor yang sama sudah ada.",
            547 => "Data ditolak oleh aturan database (misalnya stok tidak boleh negatif, atau data masih dipakai transaksi).",
            1205 => "Transaksi bertabrakan dengan pengguna lain. Silakan ulangi.",
            3201 or 3013 or 3154 or 3101 => "Backup/restore gagal. Pastikan folder dapat diakses oleh layanan SQL Server " +
                                            "(misalnya C:\\StokaraBackup) dan tidak ada pengguna lain yang sedang memakai aplikasi.",
            _ => $"Kesalahan database (kode {se.Number}). Detail tersimpan di log aplikasi."
        };
    }
}

/// <summary>Pertukaran tema terang/gelap saat runtime (FR-32).</summary>
public static class ThemeService
{
    public static string Current { get; private set; } = "Light";
    public static event Action ThemeChanged;

    public static void Apply(string theme)
    {
        theme = theme == "Dark" ? "Dark" : "Light";
        var dicts = Application.Current.Resources.MergedDictionaries;
        var fresh = new ResourceDictionary { Source = new Uri($"Resources/Themes/{theme}.xaml", UriKind.Relative) };
        var old = dicts.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("Themes/"));
        if (old != null) dicts[dicts.IndexOf(old)] = fresh;
        else dicts.Insert(0, fresh);
        Current = theme;
        ThemeChanged?.Invoke();
    }
}

/// <summary>Ekspor ke CSV yang dapat dibuka Excel (FR-28): UTF-8 BOM, pemisah titik koma, angka format Indonesia.</summary>
public static class ExportService
{
    public static void ToCsv(DataTable table, string path, string judul = null, string subjudul = null)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(judul)) sb.AppendLine(Quote(judul));
        if (!string.IsNullOrEmpty(subjudul)) sb.AppendLine(Quote(subjudul));
        if (!string.IsNullOrEmpty(judul)) sb.AppendLine();
        sb.AppendLine(string.Join(";", table.Columns.Cast<DataColumn>().Select(c => Quote(c.ColumnName))));
        foreach (DataRow row in table.Rows)
            sb.AppendLine(string.Join(";", table.Columns.Cast<DataColumn>().Select(c => Quote(Format(row[c])))));
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    private static string Format(object v) => v switch
    {
        null or DBNull => "",
        decimal d => d.ToString("0.##", Fmt.Id),
        double d => d.ToString("0.##", Fmt.Id),
        DateTime dt => dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("dd/MM/yyyy") : dt.ToString("dd/MM/yyyy HH:mm"),
        _ => v.ToString()
    };

    private static string Quote(string s)
    {
        s ??= "";
        return s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
