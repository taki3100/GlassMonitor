using System.Windows;
using System.Windows.Media;

namespace GlassMonitor;

internal static class Theme
{
    /// <summary>アクセント色のリソースを差し替える（DynamicResource で参照している箇所に即時反映）。</summary>
    public static void ApplyAccent(Color color)
    {
        var resources = Application.Current.Resources;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        resources["AccentColor"] = color;
        resources["AccentBrush"] = brush;
    }
}
