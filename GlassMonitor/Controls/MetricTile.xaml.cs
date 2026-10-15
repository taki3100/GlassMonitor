using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GlassMonitor.Controls;

/// <summary>CPU / GPU / メモリ / VRAM 共通のタイル。DataContext に TileViewModel を渡す。</summary>
public partial class MetricTile : UserControl
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(Geometry), typeof(MetricTile));

    public MetricTile()
    {
        InitializeComponent();
    }

    public Geometry? Icon
    {
        get => (Geometry?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
}
