using GlassMonitor.Localization;
using GlassMonitor.Settings;

namespace GlassMonitor.ViewModels;

public enum TemperatureLevel
{
    None,
    Normal,
    Warm,
    Hot,
}

public sealed class InfoRow(string labelKey) : ObservableObject
{
    private string _labelKey = labelKey;
    private string _value = Format.Missing;

    /// <summary>項目名のリソースキー。</summary>
    public string LabelKey
    {
        get => _labelKey;
        set
        {
            if (SetProperty(ref _labelKey, value))
                OnPropertyChanged(nameof(Label));
        }
    }

    public string Label => Localizer.Get(_labelKey);

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}

/// <summary>
/// CPU / GPU / メモリ / VRAM / ストレージの各タイル。見出しや項目名はリソースのキーで持ち、
/// 読み出すときに表示中の言語の文字列にする（言語を切り替えたら RefreshAll で読み直させる）。
/// </summary>
public class TileViewModel : ObservableObject
{
    public const int HistoryLength = 60;

    private readonly double?[] _history = new double?[HistoryLength];

    private readonly string _titleKey;
    private string _subtitle = "";
    private string _primaryLabelKey;
    private double? _usage;
    private double? _temperature;
    private TemperatureThresholds _thresholds = TemperatureThresholds.ChipDefault;
    private string _primaryValue = Format.Missing;
    private string _primaryUnit = "";
    private string? _primaryNote;
    private TemperatureLevel _temperatureLevel;
    private IReadOnlyList<double?> _historySnapshot;

    public TileViewModel(string titleKey, string primaryLabelKey, params string[] rowLabelKeys)
    {
        _titleKey = titleKey;
        _primaryLabelKey = primaryLabelKey;
        Rows = rowLabelKeys.Select(k => new InfoRow(k)).ToArray();
        _historySnapshot = (double?[])_history.Clone();
    }

    public string Title => Localizer.Get(_titleKey);

    /// <summary>大きな数字の見出し（「温度」「使用量」）。</summary>
    public string PrimaryLabel => Localizer.Get(_primaryLabelKey);

    /// <summary>リング中央の見出しのリソースキー。</summary>
    protected string UsageLabelKey { get; init; } = "Tile_Load";

    public string UsageLabel => Localizer.Get(UsageLabelKey);

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
        TemperatureLevel.Normal => Localizer.Get("Temperature_Normal"),
        TemperatureLevel.Warm => Localizer.Get("Temperature_Warm"),
        TemperatureLevel.Hot => Localizer.Get("Temperature_Hot"),
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

    /// <summary>大きな数字の見出しとサブ情報の項目名（リソースキー）を切り替える。</summary>
    public void SetLayout(string primaryLabelKey, params string[] rowLabelKeys)
    {
        if (_primaryLabelKey != primaryLabelKey)
        {
            _primaryLabelKey = primaryLabelKey;
            OnPropertyChanged(nameof(PrimaryLabel));
        }
        for (int i = 0; i < Rows.Count && i < rowLabelKeys.Length; i++)
            Rows[i].LabelKey = rowLabelKeys[i];
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
