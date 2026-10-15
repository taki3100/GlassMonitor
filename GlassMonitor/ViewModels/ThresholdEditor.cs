using System.Globalization;
using GlassMonitor.Settings;

namespace GlassMonitor.ViewModels;

/// <summary>設定画面の温度しきい値（「高め」「高温」）の入力欄。不正な値は無視して元に戻す。</summary>
public sealed class ThresholdEditor(
    string label,
    Func<TemperatureThresholds> read,
    Action<TemperatureThresholds> write) : ObservableObject
{
    public string Label { get; } = label;

    public string WarmText
    {
        get => read().Warm.ToString(CultureInfo.InvariantCulture);
        set => Apply(value, null);
    }

    public string HotText
    {
        get => read().Hot.ToString(CultureInfo.InvariantCulture);
        set => Apply(null, value);
    }

    private void Apply(string? warmText, string? hotText)
    {
        var current = read();
        int warm = current.Warm;
        int hot = current.Hot;

        if ((warmText is null || int.TryParse(warmText.Trim(), out warm))
            && (hotText is null || int.TryParse(hotText.Trim(), out hot)))
        {
            var next = new TemperatureThresholds(warm, hot);
            if (next.IsValid && next != current)
                write(next);
        }

        // 不正な入力でもテキストボックスを現在値に戻すため常に通知する
        OnPropertyChanged(nameof(WarmText));
        OnPropertyChanged(nameof(HotText));
    }
}
