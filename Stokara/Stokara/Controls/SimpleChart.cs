using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Stokara.Models;

namespace Stokara.Controls;

public enum ChartKind { Bar, Line, Progress }

/// <summary>
/// Kontrol grafik kustom (digambar sendiri dengan OnRender) - tanpa pustaka pihak ketiga.
/// Mendukung grafik batang, garis, dan progres (terpakai vs kuota), animasi tumbuh saat data berubah,
/// sorotan dan nilai saat kursor diarahkan, serta warna yang mengikuti tema (DynamicResource).
/// </summary>
public sealed class SimpleChart : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points), typeof(IEnumerable), typeof(SimpleChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure, OnPointsChanged));

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(ChartKind), typeof(SimpleChart), new FrameworkPropertyMetadata(ChartKind.Bar, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty SeriesBrushProperty = DependencyProperty.Register(
        nameof(SeriesBrush), typeof(Brush), typeof(SimpleChart), new FrameworkPropertyMetadata(Brushes.SteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(SimpleChart), new FrameworkPropertyMetadata(Brushes.LightGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GridBrushProperty = DependencyProperty.Register(
        nameof(GridBrush), typeof(Brush), typeof(SimpleChart), new FrameworkPropertyMetadata(Brushes.Gainsboro, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextBrushProperty = DependencyProperty.Register(
        nameof(TextBrush), typeof(Brush), typeof(SimpleChart), new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ValuePrefixProperty = DependencyProperty.Register(
        nameof(ValuePrefix), typeof(string), typeof(SimpleChart), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(SimpleChart), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable Points { get => (IEnumerable)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public ChartKind Kind { get => (ChartKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public Brush SeriesBrush { get => (Brush)GetValue(SeriesBrushProperty); set => SetValue(SeriesBrushProperty, value); }
    public Brush TrackBrush { get => (Brush)GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public Brush GridBrush { get => (Brush)GetValue(GridBrushProperty); set => SetValue(GridBrushProperty, value); }
    public Brush TextBrush { get => (Brush)GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
    /// <summary>Awalan nilai pada label, mis. "Rp ".</summary>
    public string ValuePrefix { get => (string)GetValue(ValuePrefixProperty); set => SetValue(ValuePrefixProperty, value); }
    /// <summary>0-1, dianimasikan saat data berubah.</summary>
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    private int _hover = -1;
    private readonly List<Rect> _hitAreas = new();
    private static readonly CultureInfo Id = CultureInfo.GetCultureInfo("id-ID");

    public SimpleChart()
    {
        MinHeight = 120;
        Cursor = Cursors.Arrow;
    }

    private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (SimpleChart)d;
        if (e.OldValue is INotifyCollectionChanged oldCol) oldCol.CollectionChanged -= chart.OnCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newCol) newCol.CollectionChanged += chart.OnCollectionChanged;
        chart.Animate();
    }

    private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateMeasure();
        Animate();
    }

    private void Animate()
    {
        BeginAnimation(ProgressProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(650))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private List<ChartPoint> Data => Points?.OfType<ChartPoint>().ToList() ?? new List<ChartPoint>();

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var p = e.GetPosition(this);
        var idx = _hitAreas.FindIndex(r => r.Contains(p));
        if (idx != _hover)
        {
            _hover = idx;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        InvalidateVisual();
    }

    private FormattedText Text(string s, double size, Brush brush, bool bold = false)
        => new(s, Id, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
            size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static string Short(double v)
    {
        var a = Math.Abs(v);
        if (a >= 1_000_000_000) return (v / 1_000_000_000).ToString("0.#", Id) + " M";
        if (a >= 1_000_000) return (v / 1_000_000).ToString("0.#", Id) + " jt";
        if (a >= 10_000) return (v / 1_000).ToString("0.#", Id) + " rb";
        return v.ToString("#,0.##", Id);
    }

    protected override void OnRender(DrawingContext dc)
    {
        _hitAreas.Clear();
        var data = Data;
        var w = ActualWidth;
        var h = ActualHeight;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h)); // agar bisa menerima event mouse
        if (w < 40 || h < 40) return;

        if (data.Count == 0)
        {
            var empty = Text("Belum ada data", 13, TextBrush);
            dc.DrawText(empty, new Point((w - empty.Width) / 2, (h - empty.Height) / 2));
            return;
        }

        switch (Kind)
        {
            case ChartKind.Progress: RenderProgress(dc, data, w); break;
            case ChartKind.Line: RenderAxes(dc, data, w, h, true); break;
            default: RenderAxes(dc, data, w, h, false); break;
        }
    }

    private void RenderAxes(DrawingContext dc, List<ChartPoint> data, double w, double h, bool line)
    {
        const double left = 52, bottom = 24, top = 18, right = 8;
        var plotW = w - left - right;
        var plotH = h - top - bottom;
        var max = Math.Max(1, data.Max(p => p.Value));
        max = NiceMax(max);

        var gridPen = new Pen(GridBrush, 1) { DashStyle = DashStyles.Dash };
        for (var i = 0; i <= 4; i++)
        {
            var y = top + plotH - plotH * i / 4.0;
            dc.DrawLine(gridPen, new Point(left, y), new Point(w - right, y));
            var lbl = Text(Short(max * i / 4.0), 10.5, TextBrush);
            dc.DrawText(lbl, new Point(left - lbl.Width - 6, y - lbl.Height / 2));
        }

        var n = data.Count;
        var slot = plotW / n;
        var labelEvery = Math.Max(1, (int)Math.Ceiling(n / Math.Max(1.0, plotW / 56)));
        var pts = new List<Point>();
        for (var i = 0; i < n; i++)
        {
            var x = left + slot * i + slot / 2;
            var vh = plotH * (data[i].Value / max) * Progress;
            var y = top + plotH - vh;
            pts.Add(new Point(x, y));
            _hitAreas.Add(new Rect(left + slot * i, top, slot, plotH));

            if (!line)
            {
                var bw = Math.Max(4, Math.Min(42, slot * 0.6));
                var brush = SeriesBrush;
                if (_hover >= 0 && _hover != i) brush = Faded(SeriesBrush);
                dc.DrawRoundedRectangle(brush, null, new Rect(x - bw / 2, y, bw, Math.Max(0, vh)), 4, 4);
            }

            if (i % labelEvery == 0)
            {
                var label = data[i].Label ?? "";
                if (label.Length > 12) label = label[..11] + "…";
                var t = Text(label, 10.5, TextBrush);
                dc.DrawText(t, new Point(x - t.Width / 2, top + plotH + 5));
            }
        }

        if (line && pts.Count > 0)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(pts[0], false, false);
                ctx.PolyLineTo(pts.Skip(1).ToList(), true, true);
            }
            var fill = new StreamGeometry();
            using (var ctx = fill.Open())
            {
                ctx.BeginFigure(new Point(pts[0].X, top + plotH), true, true);
                ctx.PolyLineTo(pts.Concat(new[] { new Point(pts[^1].X, top + plotH) }).ToList(), true, true);
            }
            var area = SeriesBrush.Clone();
            area.Opacity = 0.14;
            dc.DrawGeometry(area, null, fill);
            dc.DrawGeometry(null, new Pen(SeriesBrush, 2.4) { LineJoin = PenLineJoin.Round }, geo);
            for (var i = 0; i < pts.Count; i++)
                dc.DrawEllipse(SeriesBrush, new Pen(TrackBrush, 2), pts[i], i == _hover ? 5.5 : 3.2, i == _hover ? 5.5 : 3.2);
        }

        if (_hover >= 0 && _hover < n)
            DrawTooltip(dc, pts[_hover], $"{data[_hover].Label}: {ValuePrefix}{data[_hover].Value.ToString("#,0.##", Id)}", w);
    }

    private void RenderProgress(DrawingContext dc, List<ChartPoint> data, double w)
    {
        const double rowH = 38, barH = 10;
        for (var i = 0; i < data.Count; i++)
        {
            var p = data[i];
            var y = i * rowH + 2;
            var pct = p.Value2 <= 0 ? 0 : Math.Min(1, p.Value / p.Value2);
            var name = Text(p.Label ?? "", 12.5, TextBrush, true);
            dc.DrawText(name, new Point(0, y));
            var val = Text($"{p.Value.ToString("#,0.##", Id)} / {p.Value2.ToString("#,0.##", Id)}  ({pct:P0})", 11.5, TextBrush);
            dc.DrawText(val, new Point(w - val.Width, y + 1));
            var track = new Rect(0, y + 20, w, barH);
            dc.DrawRoundedRectangle(Faded(TrackBrush), null, track, barH / 2, barH / 2);
            var fillW = Math.Max(0, w * pct * Progress);
            var brush = pct >= 0.9 ? (Brush)Application.Current.TryFindResource("BrushDanger") ?? SeriesBrush : SeriesBrush;
            if (fillW > 0) dc.DrawRoundedRectangle(brush, null, new Rect(0, y + 20, fillW, barH), barH / 2, barH / 2);
            _hitAreas.Add(new Rect(0, y, w, rowH));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Kind != ChartKind.Progress) return base.MeasureOverride(availableSize);
        var width = double.IsInfinity(availableSize.Width) ? 300 : availableSize.Width;
        return new Size(width, Math.Max(MinHeight, Data.Count * 38 + 4));
    }

    private void DrawTooltip(DrawingContext dc, Point anchor, string text, double w)
    {
        var t = Text(text, 12, Brushes.White, true);
        var bw = t.Width + 16;
        var bh = t.Height + 8;
        var x = Math.Max(0, Math.Min(w - bw, anchor.X - bw / 2));
        var y = Math.Max(0, anchor.Y - bh - 10);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0xE6, 0x2B, 0x21, 0x18)), null, new Rect(x, y, bw, bh), 6, 6);
        dc.DrawText(t, new Point(x + 8, y + 4));
    }

    private static Brush Faded(Brush b)
    {
        var c = b.Clone();
        c.Opacity = 0.45;
        return c;
    }

    private static double NiceMax(double v)
    {
        var exp = Math.Pow(10, Math.Floor(Math.Log10(v)));
        var f = v / exp;
        var nice = f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10;
        return nice * exp;
    }
}
