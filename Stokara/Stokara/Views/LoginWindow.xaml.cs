using System.Windows;
using Stokara.ViewModels;

namespace Stokara.Views;

/// <summary>
/// Code-behind hanya menangani PasswordBox (tidak dapat di-binding demi keamanan);
/// proses login ada di LoginViewModel → AuthService.
/// </summary>
public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm = new();
    private bool _loggedIn;
    private bool _syncing;

    public LoginWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Closed += (_, _) => { if (!_loggedIn) Application.Current.Shutdown(); };
    }

    private string Password => ShowPwd.IsChecked == true ? PwdPlain.Text : PwdBox.Password;

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        if (await _vm.LoginAsync(Password))
        {
            _loggedIn = true;
            App.ShowMain();
            Close();
        }
        else
        {
            PwdBox.Focus();
            PwdBox.SelectAll();
        }
    }

    private void ShowPwd_Changed(object sender, RoutedEventArgs e)
    {
        var show = ShowPwd.IsChecked == true;
        _syncing = true;
        if (show) PwdPlain.Text = PwdBox.Password;
        else PwdBox.Password = PwdPlain.Text;
        _syncing = false;
        PwdPlain.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        PwdBox.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
    }

    private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_syncing) _vm.Error = null;
    }

    private void PwdPlain_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!_syncing) _vm.Error = null;
    }
}
