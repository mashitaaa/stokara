using System.Windows.Controls;
using System.Windows.Input;

namespace Stokara.Views;

/// <summary>Code-behind hanya untuk navigasi keyboard pada daftar saran (UI murni).</summary>
public partial class KasirView : UserControl
{
    public KasirView() => InitializeComponent();

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!SuggestList.HasItems) return;
        if (e.Key == Key.Down)
        {
            SuggestList.SelectedIndex = Math.Min(SuggestList.Items.Count - 1, SuggestList.SelectedIndex + 1);
            SuggestList.ScrollIntoView(SuggestList.SelectedItem);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            SuggestList.SelectedIndex = Math.Max(0, SuggestList.SelectedIndex - 1);
            SuggestList.ScrollIntoView(SuggestList.SelectedItem);
            e.Handled = true;
        }
    }
}
