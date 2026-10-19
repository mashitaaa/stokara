using System.Windows;
using Microsoft.Data.SqlClient;
using Stokara.Infrastructure;

namespace Stokara.Views;

/// <summary>
/// Layar pembuka: memuat konfigurasi dan menyiapkan database. Bila SQL Server tidak dapat dihubungi,
/// menampilkan langkah perbaikan yang dapat dipahami (TC-14) alih-alih menutup aplikasi.
/// </summary>
public partial class StartupWindow : Window
{
    public StartupWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await RunAsync();
        MouseLeftButtonDown += (_, _) => { try { DragMove(); } catch { /* abaikan */ } };
    }

    private async Task RunAsync()
    {
        ErrorPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        try
        {
            if (AppServices.Config == null) AppServices.Build(AppConfig.Load());
            var progress = new Progress<string>(s => StatusText.Text = s);
            await DbInitializer.EnsureAsync(AppServices.Config, progress);
            DbStatus.Set(true);
            App.ShowLogin();
            Close();
        }
        catch (Exception ex)
        {
            Logger.Error("Startup", ex);
            ProgressPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            (ErrorText.Text, DetailText.Text) = Describe(ex);
        }
    }

    private static (string, string) Describe(Exception ex)
    {
        switch (ex)
        {
            case SqlException se when ErrorHandler.IsConnectionError(se):
                return ("SQL Server tidak dapat dihubungi. Langkah perbaikan:\n" +
                        "1. Buka Services (services.msc), pastikan \"SQL Server (SQLEXPRESS)\" berstatus Running.\n" +
                        "2. Periksa connection string di appsettings.json (nama server, mis. .\\SQLEXPRESS).\n" +
                        "3. Tekan Coba lagi.",
                    "Detail teknis tersimpan di " + Logger.LogFolder);
            case SqlException se:
                return ("Terjadi kesalahan saat menyiapkan database (kode " + se.Number + ").",
                    se.Message + "\nDetail tersimpan di " + Logger.LogFolder);
            case FileNotFoundException fe:
                return ("File pendukung tidak ditemukan: " + Path.GetFileName(fe.FileName) +
                        ". Pastikan folder Database berisi schema.sql dan seed.sql.", fe.FileName);
            case InvalidOperationException io:
                return (io.Message, "Periksa file appsettings.json di folder aplikasi.");
            default:
                return ("Aplikasi gagal dimulai.", ex.Message + "\nDetail tersimpan di " + Logger.LogFolder);
        }
    }

    private async void Retry_Click(object sender, RoutedEventArgs e) => await RunAsync();

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
