using GlassMonitor.Settings;

namespace GlassMonitor.ViewModels;

public enum TemperatureLevel
{
    None,
    Normal,
    Warm,
    Hot,
}

public sealed class InfoRow(string label) : ObservableObject
{
    private string _label = label;
    private string _value = Format.Missing;

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}

/// <summary>CPU / GPU / メモリ / VRAM / ストレージの各タイル。</summary>
public class TileViewModel : ObservableObject
{
    public const int HistoryLength = 60;

    private readonly double?[] _history = new double?[HistoryLength];

    private string _subtitle = "";
    private string _primaryLabel;
    private double? _usage;
    private double? _temperature;
    private TemperatureThresholds _thresholds = TemperatureThresholds.ChipDefault;
    private string _primaryValue = Format.Missing;
    private string _primaryUnit = "";
    private string? _primaryNote;
    private TemperatureLevel _temperatureLevel;
    private IReadOnlyList<double?> _historySnapshot;

    public TileViewModel(string title, string primaryLabel, params string[] rowLabels)
    {
        Title = title;
        _primaryLabel = primaryLabel;
        Rows = rowLabels.Select(l => new InfoRow(l)).ToArray();
        _historySnapshot = (double?[])_history.Clone();
    }

    public string Title { get; }

    /// <summary>大きな数字の見出し（「温度」「使用量」）。</summary>
    public string PrimaryLabel
    {
        get => _primaryLabel;
        private set => SetProperty(ref _primaryLabel, value);
    }

    /// <summary>リング中央の見出し。</summary>
    public string UsageLabel { get; init; } = "使用率";

    public IReadOnlyList<InfoRow> Rows { get; }

    public string Subtitle
    {
        get => _subtitle;
        set => SetProperty(ref _subtitle, value);
    }

    /// <summary>リングに表示する使用率（0〜100）。</summary>
    public double? Usage
    {
        get => _usage;
        private set
        {
            if (SetProperty(ref _usage, value))
            {
                OnPropertyChanged(nameof(UsageText));
                OnPropertyChanged(nameof(UsageUnit));
            }
        }
    }

    public string UsageText => Format.Number(Usage, "0");

    public string UsageUnit => Usage is null ? "" : "%";

    /// <summary>大きく表示する値（温度、VRAM は使用量）。</summary>
    public string PrimaryValue
    {
        get => _primaryValue;
        private set => SetProperty(ref _primaryValue, value);
    }

    public string PrimaryUnit
    {
        get => _primaryUnit;
        private set => SetProperty(ref _primaryUnit, value);
    }

    /// <summary>大きな値の下の補足（VRAM の「/ 16.0 GB」）。</summary>
    public string? PrimaryNote
    {
        get => _primaryNote;
        private set => SetProperty(ref _primaryNote, value);
    }

    /// <summary>温度の判定に使うしきい値。変えるとすぐに判定し直す。</summary>
    public TemperatureThresholds Thresholds
    {
        get => _thresholds;
        set
        {
            _thresholds = value;
            TemperatureLevel = Classify(_temperature);
        }
    }

    public TemperatureLevel TemperatureLevel
    {
        get => _temperatureLevel;
        private set
        {
            if (SetProperty(ref _temperatureLevel, value))
                OnPropertyChanged(nameof(TemperatureText));
        }
    }

    public string TemperatureText => TemperatureLevel switch
    {
        TemperatureLevel.Normal => "適温",
        TemperatureLevel.Warm => "高め",
        TemperatureLevel.Hot => "高温",
        _ => "",
    };

    /// <summary>直近 60 秒の使用率。末尾が最新で、まだ記録の無い秒は null。</summary>
    public IReadOnlyList<double?> History
    {
        get => _historySnapshot;
        private set => SetProperty(ref _historySnapshot, value);
    }

    public void PushUsage(double? percent)
    {
        Usage = percent is double p ? Math.Clamp(p, 0, 100) : null;
        History = Push(_history, Usage);
    }

    public void SetTemperature(double? celsius)
    {
        // 表示と判定がずれないよう、丸めた値で判定する（69.6℃ は「70」なので「高め」）
        _temperature = celsius is double c ? Math.Round(c) : null;
        PrimaryValue = Format.Number(_temperature, "0");
        PrimaryUnit = _temperature is null ? "" : "°C";
        PrimaryNote = null;
        TemperatureLevel = Classify(_temperature);
    }

    /// <summary>温度以外の値を大きく表示する（VRAM の使用量など）。温度バッジは消える。</summary>
    public void SetPrimary(double? value, string format, string unit, string? note)
    {
        _temperature = null;
        PrimaryValue = Format.Number(value, format);
        PrimaryUnit = value is null ? "" : unit;
        PrimaryNote = note;
        TemperatureLevel = TemperatureLevel.None;
    }

    /// <summary>大きな数字の見出しとサブ情報の項目名を切り替える。</summary>
    public void SetLayout(string primaryLabel, params string[] rowLabels)
    {
        PrimaryLabel = primaryLabel;
        for (int i = 0; i < Rows.Count && i < rowLabels.Length; i++)
            Rows[i].Label = rowLabels[i];
    }

    public void SetRows(params string[] values)
    {
        for (int i = 0; i < Rows.Count && i < values.Length; i++)
            Rows[i].Value = values[i];
    }

    /// <summary>末尾に値を追加して古い値を押し出し、バインド用のコピーを返す。</summary>
    protected static double?[] Push(double?[] buffer, double? value)
    {
        Array.Copy(buffer, 1, buffer, 0, buffer.Length - 1);
        buffer[^1] = value;
        return (double?[])buffer.Clone();
    }

    private TemperatureLevel Classify(double? celsius) => ClassifyTemperature(celsius, _thresholds);

    /// <summary>丸め済みの温度をしきい値で判定する。</summary>
    public static TemperatureLevel ClassifyTemperature(double? celsius, TemperatureThresholds thresholds) => celsius switch
    {
        null => TemperatureLevel.None,
        double t when t < thresholds.Warm => TemperatureLevel.Normal,
        double t when t < thresholds.Hot => TemperatureLevel.Warm,
        _ => TemperatureLevel.Hot,
    };
}
