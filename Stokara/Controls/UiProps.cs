using System.Collections;
using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Stokara.Controls;

/// <summary>Properti terlampir untuk gaya kontrol: ikon tombol, placeholder input, dan fokus otomatis.</summary>
public static class UiProps
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached(
        "Icon", typeof(string), typeof(UiProps), new PropertyMetadata(null));

    public static string GetIcon(DependencyObject d) => (string)d.GetValue(IconProperty);
    public static void SetIcon(DependencyObject d, string v) => d.SetValue(IconProperty, v);

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.RegisterAttached(
        "Placeholder", typeof(string), typeof(UiProps), new PropertyMetadata(null));

    public static string GetPlaceholder(DependencyObject d) => (string)d.GetValue(PlaceholderProperty);
    public static void SetPlaceholder(DependencyObject d, string v) => d.SetValue(PlaceholderProperty, v);

    /// <summary>Memberi fokus keyboard saat kontrol dimuat (kolom barcode kasir, login).</summary>
    public static readonly DependencyProperty FocusOnLoadProperty = DependencyProperty.RegisterAttached(
        "FocusOnLoad", typeof(bool), typeof(UiProps), new PropertyMetadata(false, OnFocusOnLoad));

    public static bool GetFocusOnLoad(DependencyObject d) => (bool)d.GetValue(FocusOnLoadProperty);
    public static void SetFocusOnLoad(DependencyObject d, bool v) => d.SetValue(FocusOnLoadProperty, v);

    private static void OnFocusOnLoad(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement fe && (bool)e.NewValue)
            fe.Loaded += (_, _) => fe.Dispatcher.BeginInvoke(new Action(() => { fe.Focus(); Keyboard.Focus(fe); }),
                System.Windows.Threading.DispatcherPriority.Input);
    }

    /// <summary>
    /// Kolom DataGrid otomatis untuk DataTable laporan: binding lewat indexer (aman untuk nama kolom berspasi)
    /// dan format angka/tanggal Indonesia.
    /// </summary>
    public static readonly DependencyProperty SmartColumnsProperty = DependencyProperty.RegisterAttached(
        "SmartColumns", typeof(bool), typeof(UiProps), new PropertyMetadata(false, OnSmartColumns));

    public static bool GetSmartColumns(DependencyObject d) => (bool)d.GetValue(SmartColumnsProperty);
    public static void SetSmartColumns(DependencyObject d, bool v) => d.SetValue(SmartColumnsProperty, v);

    private static void OnSmartColumns(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid || !(bool)e.NewValue) return;
        grid.AutoGeneratingColumn += (_, args) =>
        {
            var type = args.PropertyType;
            var binding = new Binding($"[{args.PropertyName}]");
            var numeric = type == typeof(decimal) || type == typeof(double) || type == typeof(int) || type == typeof(long);
            if (numeric) binding.StringFormat = "#,0.##";
            else if (type == typeof(DateTime)) binding.StringFormat = "dd/MM/yyyy HH:mm";
            var col = new DataGridTextColumn { Header = args.PropertyName, Binding = binding, SortMemberPath = args.PropertyName };
            if (numeric)
            {
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
                col.ElementStyle = style;
            }
            args.Column = col;
        };
    }
}

/// <summary>Animasi masuk halaman (fade + geser) saat konten berganti - transisi halaman Rich UI.</summary>
public static class PageTransition
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(PageTransition), new PropertyMetadata(false, OnEnabled));

    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject d, bool v) => d.SetValue(EnabledProperty, v);

    private static void OnEnabled(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ContentControl cc || !(bool)e.NewValue) return;
        var descriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(ContentControl.ContentProperty, typeof(ContentControl));
        descriptor.AddValueChanged(cc, (_, _) => Play(cc));
    }

    private static void Play(FrameworkElement el)
    {
        var translate = new System.Windows.Media.TranslateTransform(0, 14);
        el.RenderTransform = translate;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        el.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = ease });
        translate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(300)) { EasingFunction = ease });
    }
}

// ============================== Converters ==============================

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var b = value is bool v && v;
        if (parameter as string == "Invert") b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => !(value is bool b && b);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => !(value is bool b && b);
}

/// <summary>Visible bila nilai tidak null/kosong (parameter "Invert" untuk kebalikannya).</summary>
public sealed class NotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var has = value switch
        {
            null => false,
            string s => !string.IsNullOrWhiteSpace(s),
            int i => i > 0,
            ICollection c => c.Count > 0,
            IEnumerable en => en.Cast<object>().Any(),
            _ => true
        };
        if (parameter as string == "Invert") has = !has;
        return has ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Membandingkan nilai dengan parameter string (untuk RadioButton/segmen).</summary>
public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? parameter?.ToString() : Binding.DoNothing;
}

/// <summary>Format Rupiah: 75000 → "Rp 75.000".</summary>
public sealed class RupiahConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value switch
        {
            decimal d => "Rp " + d.ToString("N0", Infrastructure.Fmt.Id),
            double d => "Rp " + d.ToString("N0", Infrastructure.Fmt.Id),
            int i => "Rp " + i.ToString("N0", Infrastructure.Fmt.Id),
            _ => ""
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Angka lebar (0-100) ke lebar piksel proporsional, untuk bar progres sederhana.</summary>
public sealed class PercentToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double pct || values[1] is not double width) return 0d;
        return Math.Max(0, Math.Min(1, pct / 100.0)) * width;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => null;
}

/// <summary>Indeks berbasis 0 → nomor langkah berbasis 1 (panduan).</summary>
public sealed class StepNumberConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i ? (i + 1).ToString() : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
