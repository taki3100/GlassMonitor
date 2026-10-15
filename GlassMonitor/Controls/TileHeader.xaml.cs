using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GlassMonitor.Controls;

/// <summary>タイル左上のアイコン・タイトル・サブタイトル。</summary>
public partial class TileHeader : UserControl
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(Geometry), typeof(TileHeader));

    public TileHeader()
    {
        InitializeComponent();
    }

    public Geometry? Icon
    {
        get => (Geometry?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
}
