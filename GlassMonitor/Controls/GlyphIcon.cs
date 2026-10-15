using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GlassMonitor.Controls;

/// <summary>
/// 24×24 座標系で定義した線画アイコン。Foreground で着色し、テンプレートは App.xaml にある。
/// </summary>
public sealed class GlyphIcon : Control
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(Geometry), typeof(GlyphIcon));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(GlyphIcon), new PropertyMetadata(2.0));

    static GlyphIcon()
    {
        FocusableProperty.OverrideMetadata(typeof(GlyphIcon), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(GlyphIcon), new FrameworkPropertyMetadata(false));
    }

    public Geometry? Data
    {
        get => (Geometry?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }
}
