using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Stokara.Infrastructure;

namespace Stokara.Controls;

/// <summary>
/// Kartu statistik dashboard (custom control dengan template di Styles.xaml).
/// Nilai dianimasikan dari angka lama ke angka baru setiap kali data dimuat ulang.
/// </summary>
public sealed class StatCard : Control
{
    // Template diberikan oleh implicit style TargetType="c:StatCard" di Resources/Styles.xaml.

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(StatCard));
    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(StatCard));
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(string), typeof(StatCard));
    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(StatCard));
    /// <summary>"Rupiah" atau "Angka".</summary>
    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(nameof(Format), typeof(string), typeof(StatCard),
        new PropertyMetadata("Angka", (d, _) => ((StatCard)d).UpdateText()));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(StatCard),
        new PropertyMetadata(0d, OnValueChanged));

    public static readonly DependencyProperty AnimatedValueProperty = DependencyProperty.Register(nameof(AnimatedValue), typeof(double), typeof(StatCard),
        new PropertyMetadata(0d, (d, _) => ((StatCard)d).UpdateText()));

    private static readonly DependencyPropertyKey DisplayTextKey = DependencyProperty.RegisterReadOnly(nameof(DisplayText), typeof(string),
        typeof(StatCard), new PropertyMetadata("0"));
    public static readonly DependencyProperty DisplayTextProperty = DisplayTextKey.DependencyProperty;

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public Brush Accent { get => (Brush)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public string Format { get => (string)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double AnimatedValue { get => (double)GetValue(AnimatedValueProperty); set => SetValue(AnimatedValueProperty, value); }
    public string DisplayText => (string)GetValue(DisplayTextProperty);

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (StatCard)d;
        card.BeginAnimation(AnimatedValueProperty, new DoubleAnimation((double)e.OldValue, (double)e.NewValue, TimeSpan.FromMilliseconds(700))
        {
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void UpdateText()
    {
        var v = (decimal)Math.Round(AnimatedValue, 0);
        SetValue(DisplayTextKey, Format == "Rupiah" ? Fmt.Rp(v) : v.ToString("N0", Fmt.Id));
    }
}
