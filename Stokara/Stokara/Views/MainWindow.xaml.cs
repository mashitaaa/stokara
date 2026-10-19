using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Stokara.Infrastructure;
using Stokara.ViewModels;

namespace Stokara.Views;

/// <summary>
/// Jendela utama. Code-behind hanya berisi perilaku UI murni: pintasan keyboard global,
/// fokus ke kolom pencarian, tutup toast, logout, dan logout otomatis bila menganggur.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private bool _loggingOut;
    private DateTime _lastInput = DateTime.Now;
    private readonly DispatcherTimer _idleTimer;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
        PreviewKeyDown += OnPreviewKeyDown;
        Closed += OnClosed;
        PengaturanViewModel.RestoreCompleted += OnRestoreCompleted;

        // Logout otomatis setelah tidak aktif (opsional, appsettings.json → IdleLogoutMinutes).
        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        if (AppServices.Config.IdleLogoutMinutes > 0)
        {
            InputManager.Current.PreProcessInput += OnAnyInput;
            _idleTimer.Tick += async (_, _) =>
            {
                if ((DateTime.Now - _lastInput).TotalMinutes >= AppServices.Config.IdleLogoutMinutes)
                {
                    ToastService.Info("Sesi berakhir karena tidak ada aktivitas.");
                    await LogoutAsync();
                }
            };
            _idleTimer.Start();
        }
    }

    private void OnAnyInput(object sender, PreProcessInputEventArgs e)
    {
        if (e.StagingItem.Input is MouseEventArgs or KeyboardEventArgs) _lastInput = DateTime.Now;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        if (ctrl && e.Key == Key.N)
        {
            _vm.ShortcutNew();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.S)
        {
            CommitFocusedText();
            _vm.ShortcutSave();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.F)
        {
            FocusSearchBox();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && Keyboard.FocusedElement is not ComboBox)
        {
            _vm.ShortcutEscape();
        }
    }

    /// <summary>Memastikan isian TextBox yang sedang difokus terkirim ke ViewModel sebelum disimpan.</summary>
    private static void CommitFocusedText()
    {
        if (Keyboard.FocusedElement is TextBox tb)
            tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    private void FocusSearchBox()
    {
        var box = FindNamed(PageHost, "SearchBox");
        if (box == null) return;
        box.Focus();
        Keyboard.Focus(box);
        if (box is TextBox tb) tb.SelectAll();
    }

    private static FrameworkElement FindNamed(DependencyObject root, string name)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement fe && fe.Name == name && fe.IsVisible) return fe;
            var found = FindNamed(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private void DismissToast_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Toast t) ToastService.Dismiss(t);
    }

    private async void Logout_Click(object sender, RoutedEventArgs e)
    {
        if (!DialogService.Confirm("Keluar dari STOKARA?", "Keluar", "Keluar")) return;
        await LogoutAsync();
    }

    private void OnRestoreCompleted() => _ = Dispatcher.InvokeAsync(async () => await LogoutAsync());

    private async Task LogoutAsync()
    {
        if (_loggingOut) return;
        _loggingOut = true;
        try { await _vm.LogoutAsync(); }
        catch (Exception ex) { Logger.Error("Logout", ex); Session.End(); }
        App.ShowLogin();
        Close();
    }

    private void OnClosed(object sender, EventArgs e)
    {
        _idleTimer.Stop();
        InputManager.Current.PreProcessInput -= OnAnyInput;
        PengaturanViewModel.RestoreCompleted -= OnRestoreCompleted;
        _vm.Stop();
        if (!_loggingOut)
        {
            _ = AppServices.Auth.LogoutAsync();
            Application.Current.Shutdown();
        }
    }
}
