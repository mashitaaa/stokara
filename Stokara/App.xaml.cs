using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Stokara.Infrastructure;
using Stokara.Views;

namespace Stokara;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Bahasa Indonesia untuk format angka, tanggal, dan Rupiah (NFR-10).
        var id = CultureInfo.GetCultureInfo("id-ID");
        CultureInfo.DefaultThreadCurrentCulture = id;
        CultureInfo.DefaultThreadCurrentUICulture = id;
        Thread.CurrentThread.CurrentCulture = id;
        Thread.CurrentThread.CurrentUICulture = id;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(id.IetfLanguageTag)));

        // Error yang lolos tidak menutup aplikasi secara tidak terkendali (FR-31).
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Logger.Error("Task", args.Exception);
            args.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex) Logger.Error("Fatal", ex);
        };

        ThemeService.Apply("Light");
        new StartupWindow().Show();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ErrorHandler.Handle(e.Exception);
        e.Handled = true;
    }

    public static void ShowLogin()
    {
        ThemeService.Apply("Light");
        var login = new LoginWindow();
        Current.MainWindow = login;
        login.Show();
    }

    public static void ShowMain()
    {
        ThemeService.Apply(Session.Current?.Tema ?? "Light");
        var main = new MainWindow();
        Current.MainWindow = main;
        main.Show();
    }
}
