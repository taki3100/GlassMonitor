using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GlassMonitor.Controls;

/// <summary>ストレージのタイル。DataContext に StorageTileViewModel を渡す。</summary>
public partial class StorageTile : UserControl
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(Geometry), typeof(StorageTile));

    public StorageTile()
    {
        InitializeComponent();
    }

    public Geometry? Icon
    {
        get => (Geometry?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
}
