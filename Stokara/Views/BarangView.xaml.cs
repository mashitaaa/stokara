using System.Windows.Controls;
using System.Windows.Input;
using Stokara.ViewModels;

namespace Stokara.Views;

/// <summary>
/// Pengurutan diteruskan ke server (ORDER BY di repository) agar berlaku untuk semua halaman, bukan hanya halaman aktif.
/// </summary>
public partial class BarangView : UserControl
{
    public BarangView() => InitializeComponent();

    private void Grid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        if (DataContext is BarangViewModel vm && vm.SortCommand.CanExecute(e.Column.SortMemberPath))
            vm.SortCommand.Execute(e.Column.SortMemberPath);
    }

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is BarangViewModel vm && vm.EditCommand.CanExecute(null)) vm.EditCommand.Execute(null);
    }
}
