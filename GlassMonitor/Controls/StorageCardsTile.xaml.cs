using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GlassMonitor.Controls;

/// <summary>ディスクが 2〜4 台のときのストレージのタイル。DataContext に StorageDrivesViewModel を渡す。</summary>
public partial class StorageCardsTile : UserControl
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(Geometry), typeof(StorageCardsTile));

    public StorageCardsTile()
    {
        InitializeComponent();
    }

    public Geometry? Icon
    {
        get => (Geometry?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
}
