using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Path = System.Windows.Shapes.Path;

namespace GlassMonitor.Controls;

/// <summary>
/// 直近の履歴を描く折れ線。線と塗りの 2 つの Path を自前で組み立てる。
/// 値は先頭が最古・末尾が最新で、null の区間は線を途切れさせる。
/// </summary>
public sealed class Sparkline : Grid
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double?>), typeof(Sparkline), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(Sparkline), new PropertyMetadata(100.0, OnChanged));

    public static readonly DependencyProperty AccentColorProperty = DependencyProperty.Register(
        nameof(AccentColor), typeof(Color), typeof(Sparkline), new PropertyMetadata(Colors.White, OnAccentChanged));

    public static readonly DependencyProperty ShowAreaProperty = DependencyProperty.Register(
        nameof(ShowArea), typeof(bool), typeof(Sparkline), new PropertyMetadata(true, OnStyleChanged));

    public static readonly DependencyProperty IsDashedProperty = DependencyProperty.Register(
        nameof(IsDashed), typeof(bool), typeof(Sparkline), new PropertyMetadata(false, OnStyleChanged));

    // 線の太さと発光が上端で切れないように確保する余白
    private const double TopPadding = 3;

    private readonly Path _area = new();

    private readonly Path _line = new()
    {
        StrokeThickness = 1.6,
        StrokeLineJoin = PenLineJoin.Round,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
    };

    private readonly DropShadowEffect _glow = new() { ShadowDepth = 0, BlurRadius = 8, Opacity = 0.7 };

    public Sparkline()
    {
        _line.Effect = _glow;
        Children.Add(_area);
        Children.Add(_line);
        SizeChanged += (_, _) => Redraw();
        ApplyAccent();
    }

    public IReadOnlyList<double?>? Values
    {
        get => (IReadOnlyList<double?>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    /// <summary>線の下を塗るか。</summary>
    public bool ShowArea
    {
        get => (bool)GetValue(ShowAreaProperty);
        set => SetValue(ShowAreaProperty, value);
    }

    /// <summary>破線で描くか。</summary>
    public bool IsDashed
    {
        get => (bool)GetValue(IsDashedProperty);
        set => SetValue(IsDashedProperty, value);
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((Sparkline)d).Redraw();

    private static void OnStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var sparkline = (Sparkline)d;
        sparkline._area.Visibility = sparkline.ShowArea ? Visibility.Visible : Visibility.Collapsed;
        sparkline._line.StrokeDashArray = sparkline.IsDashed ? new DoubleCollection { 2.2, 2 } : null;
    }

    private static void OnAccentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((Sparkline)d).ApplyAccent();

    private void ApplyAccent()
    {
        var color = AccentColor;
        var stroke = new SolidColorBrush(color);
        stroke.Freeze();

        var fill = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        fill.GradientStops.Add(new GradientStop(Color.FromArgb(0x55, color.R, color.G, color.B), 0));
        fill.GradientStops.Add(new GradientStop(Color.FromArgb(0x1A, color.R, color.G, color.B), 0.6));
        fill.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, color.R, color.G, color.B), 1));
        fill.Freeze();

        _line.Stroke = stroke;
        _area.Fill = fill;
        _glow.Color = color;
    }

    private void Redraw()
    {
        var values = Values;
        double width = ActualWidth;
        double height = ActualHeight;
        if (values is null || values.Count < 2 || width <= 0 || height <= TopPadding)
        {
            _line.Data = null;
            _area.Data = null;
            return;
        }

        double plotHeight = height - TopPadding - 1;
        double step = width / (values.Count - 1);
        double max = Maximum > 0 ? Maximum : 100;

        var line = new StreamGeometry();
        var area = new StreamGeometry();
        using (var lineCtx = line.Open())
        using (var areaCtx = area.Open())
        {
            var run = new List<Point>(values.Count);
            for (int i = 0; i <= values.Count; i++)
            {
                if (i < values.Count && values[i] is double v)
                {
                    double y = TopPadding + plotHeight * (1 - Math.Clamp(v / max, 0, 1));
                    run.Add(new Point(i * step, y));
                    continue;
                }

                if (run.Count >= 2)
                    AddRun(lineCtx, areaCtx, run, TopPadding, TopPadding + plotHeight, height);
                run.Clear();
            }
        }
        line.Freeze();
        area.Freeze();

        _line.Data = line;
        _area.Data = area;
    }

    /// <summary>連続した点列を Catmull-Rom 由来のベジェ曲線でなめらかにつなぐ。</summary>
    private static void AddRun(
        StreamGeometryContext lineCtx, StreamGeometryContext areaCtx, List<Point> points,
        double minY, double maxY, double bottom)
    {
        lineCtx.BeginFigure(points[0], isFilled: false, isClosed: false);
        areaCtx.BeginFigure(new Point(points[0].X, bottom), isFilled: true, isClosed: true);
        areaCtx.LineTo(points[0], isStroked: false, isSmoothJoin: false);

        for (int i = 0; i < points.Count - 1; i++)
        {
            var p0 = points[Math.Max(i - 1, 0)];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = points[Math.Min(i + 2, points.Count - 1)];

            // 制御点は描画範囲内に収めて、0% / 100% 付近で線がはみ出さないようにする
            var c1 = new Point(p1.X + (p2.X - p0.X) / 6, Math.Clamp(p1.Y + (p2.Y - p0.Y) / 6, minY, maxY));
            var c2 = new Point(p2.X - (p3.X - p1.X) / 6, Math.Clamp(p2.Y - (p3.Y - p1.Y) / 6, minY, maxY));

            lineCtx.BezierTo(c1, c2, p2, isStroked: true, isSmoothJoin: true);
            areaCtx.BezierTo(c1, c2, p2, isStroked: false, isSmoothJoin: true);
        }

        areaCtx.LineTo(new Point(points[^1].X, bottom), isStroked: false, isSmoothJoin: false);
    }
}
