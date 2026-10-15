using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Path = System.Windows.Shapes.Path;

namespace GlassMonitor.Controls;

/// <summary>使用率リング。トラックと進捗の円弧を Path で描画する（12 時の位置から時計回り）。</summary>
public sealed class RingGauge : Grid
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double?), typeof(RingGauge), new PropertyMetadata(null, OnValueChanged));

    public static readonly DependencyProperty AccentColorProperty = DependencyProperty.Register(
        nameof(AccentColor), typeof(Color), typeof(RingGauge), new PropertyMetadata(Colors.White, OnAccentChanged));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(RingGauge), new PropertyMetadata(10.0, OnGeometryChanged));

    // Value への変化をアニメーションさせるための内部プロパティ
    private static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        "Progress", typeof(double), typeof(RingGauge), new PropertyMetadata(0.0, OnGeometryChanged));

    private readonly Path _track = new() { Stroke = new SolidColorBrush(Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF)) };

    private readonly Path _arc = new()
    {
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
    };

    private readonly DropShadowEffect _glow = new() { ShadowDepth = 0, BlurRadius = 14, Opacity = 0.75 };

    public RingGauge()
    {
        _arc.Effect = _glow;
        Children.Add(_track);
        Children.Add(_arc);
        SizeChanged += (_, _) => Redraw();
        ApplyAccent();
    }

    /// <summary>0〜100。null のときは円弧を描かない。</summary>
    public double? Value
    {
        get => (double?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var target = e.NewValue is double v ? Math.Clamp(v, 0, 100) : 0;
        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(600))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        ((RingGauge)d).BeginAnimation(ProgressProperty, animation);
    }

    private static void OnAccentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((RingGauge)d).ApplyAccent();

    private static void OnGeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((RingGauge)d).Redraw();

    private void ApplyAccent()
    {
        var brush = new SolidColorBrush(AccentColor);
        brush.Freeze();
        _arc.Stroke = brush;
        _glow.Color = AccentColor;
    }

    private void Redraw()
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0)
            return;

        double thickness = Thickness;
        double radius = (size - thickness) / 2;
        var center = new Point(ActualWidth / 2, ActualHeight / 2);

        _track.StrokeThickness = thickness;
        _arc.StrokeThickness = thickness;
        _track.Data = new EllipseGeometry(center, radius, radius);
        _arc.Data = BuildArc(center, radius, (double)GetValue(ProgressProperty));
    }

    private static Geometry? BuildArc(Point center, double radius, double percent)
    {
        if (percent < 0.1)
            return null;
        if (percent > 99.9)
            return new EllipseGeometry(center, radius, radius);

        double angle = percent / 100 * 2 * Math.PI;
        var start = new Point(center.X, center.Y - radius);
        var end = new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle));

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, isFilled: false, isClosed: false);
            ctx.ArcTo(end, new Size(radius, radius), 0, isLargeArc: percent > 50, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: true);
        }
        geometry.Freeze();
        return geometry;
    }
}
