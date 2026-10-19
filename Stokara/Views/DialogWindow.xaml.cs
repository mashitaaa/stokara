using System.Windows;

namespace Stokara.Views;

/// <summary>Dialog konfirmasi / isian bertema untuk aksi berisiko (FR-30).</summary>
public partial class DialogWindow : Window
{
    private readonly bool _hasInput;

    public DialogWindow(string title, string message, string okText, bool danger, string inputLabel, string initial)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        OkButton.Content = okText;
        if (danger)
        {
            OkButton.Style = (Style)FindResource("ButtonDanger");
            IconHost.SetResourceReference(BackgroundProperty, "BadgeHabisBg");
            IconText.SetResourceReference(ForegroundProperty, "BadgeHabisFg");
            IconText.Text = "";
        }
        _hasInput = inputLabel != null;
        if (_hasInput)
        {
            InputPanel.Visibility = Visibility.Visible;
            InputLabel.Text = inputLabel;
            InputBox.Text = initial ?? "";
            Loaded += (_, _) => { InputBox.Focus(); InputBox.SelectAll(); };
        }
        else
        {
            Loaded += (_, _) => CancelButton.Focus();
        }
        MouseLeftButtonDown += (_, _) => { try { DragMove(); } catch { /* abaikan */ } };
    }

    public string InputText => InputBox.Text?.Trim();

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (_hasInput && string.IsNullOrWhiteSpace(InputBox.Text))
        {
            InputError.Visibility = Visibility.Visible;
            InputBox.Focus();
            return;
        }
        DialogResult = true;
    }

    private void InputBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => InputError.Visibility = Visibility.Collapsed;
}
