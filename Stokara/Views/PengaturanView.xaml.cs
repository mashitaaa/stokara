using System.Windows;
using System.Windows.Controls;
using Stokara.ViewModels;

namespace Stokara.Views;

/// <summary>Code-behind hanya meneruskan isi PasswordBox (tidak dapat di-binding) ke ViewModel.</summary>
public partial class PengaturanView : UserControl
{
    public PengaturanView() => InitializeComponent();

    private async void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PengaturanViewModel vm) return;
        if (await vm.ChangePasswordAsync(PwdOld.Password, PwdNew.Password, PwdRepeat.Password))
        {
            PwdOld.Clear();
            PwdNew.Clear();
            PwdRepeat.Clear();
        }
    }
}
