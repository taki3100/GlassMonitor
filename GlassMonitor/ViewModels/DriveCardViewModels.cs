using System.Collections.ObjectModel;
using GlassMonitor.Services;
using GlassMonitor.Settings;

namespace GlassMonitor.ViewModels;

/// <summary>ディスクが 2〜4 台のときに、ストレージのタイルを等分して並べるカード 1 枚分。</summary>
public sealed class DriveCardViewModel(string key) : ObservableObject
{
    /// <summary>空き容量がこの割合（%）未満なら「空き少」。</summary>
    private const double LowSpacePercent = 10;

    private string _letters = "";
    private string _label = "";
    private string _description = "";
    private string _temperatureText = Format.Missing;
    private string _temperatureUnit = "";
    private TemperatureLevel _temperatureLevel;
    private double _usedPercent;
    private string _capacityText = Format.Missing;
    private string _usedPercentText = Format.Missing;
    private bool _isLowSpace;
    private string _throughputText = Format.Missing;
    private string _healthText = Format.Missing;
    private bool _isHealthCaution;

    public string Key { get; } = key;

    /// <summary>"C:" / "D: E:"（ドライブ文字が無ければ「ディスク n」）。</summary>
    public string Letters { get => _letters; private set => SetProperty(ref _letters, value); }

    /// <summary>ボリュームラベル。</summary>
    public string Label { get => _label; private set => SetProperty(ref _label, value); }

    /// <summary>"WD_BLACK SN8100 · NVMe Gen5" のような型番と種類。</summary>
    public string Description { get => _description; private set => SetProperty(ref _description, value); }

    public string TemperatureText { get => _temperatureText; private set => SetProperty(ref _temperatureText, value); }

    public string TemperatureUnit { get => _temperatureUnit; private set => SetProperty(ref _temperatureUnit, value); }

    public TemperatureLevel TemperatureLevel { get => _temperatureLevel; private set => SetProperty(ref _temperatureLevel, value); }

    /// <summary>容量バーの長さ（0〜100）。</summary>
    public double UsedPercent { get => _usedPercent; private set => SetProperty(ref _usedPercent, value); }

    /// <summary>"0.94 / 2.00 TB"</summary>
    public string CapacityText { get => _capacityText; private set => SetProperty(ref _capacityText, value); }

    /// <summary>"47%"、空き容量が少なければ "92% 空き少"。</summary>
    public string UsedPercentText { get => _usedPercentText; private set => SetProperty(ref _usedPercentText, value); }

    public bool IsLowSpace { get => _isLowSpace; private set => SetProperty(ref _isLowSpace, value); }

    /// <summary>"1,240 / 380 MB/s"（読込 / 書込）</summary>
    public string ThroughputText { get => _throughputText; private set => SetProperty(ref _throughputText, value); }

    /// <summary>"99%"（寿命）または "良好" / "注意"（SMART の状態）。</summary>
    public string HealthText { get => _healthText; private set => SetProperty(ref _healthText, value); }

    public bool IsHealthCaution { get => _isHealthCaution; private set => SetProperty(ref _isHealthCaution, value); }

    public void Update(DriveReading drive, TemperatureThresholds thresholds)
    {
        Letters = drive.Letters ?? $"ディスク {drive.DriveNumber}";
        Label = drive.Label ?? "";
        Description = string.Join(" · ", new[] { drive.Model, drive.Kind }.Where(s => !string.IsNullOrEmpty(s)));

        // USB 接続のドライブなど温度が取れないものは「—」
        double? temperature = drive.Temperature is double t ? Math.Round(t) : null;
        TemperatureText = Format.Number(temperature, "0");
        TemperatureUnit = temperature is null ? "" : "°C";
        TemperatureLevel = TileViewModel.ClassifyTemperature(temperature, thresholds);

        bool terabytes = drive.TotalGb >= 1000;
        UsedPercent = drive.UsedPercent is double used ? Math.Clamp(used, 0, 100) : 0;
        CapacityText = drive.TotalGb is double total
            ? $"{Format.Number(Scale(drive.UsedGb, terabytes), terabytes ? "0.00" : "0")} / {Format.Capacity(total, terabytes)}"
            : Format.Missing;
        IsLowSpace = drive.FreePercent < LowSpacePercent;
        UsedPercentText = drive.UsedPercent is null
            ? Format.Missing
            : IsLowSpace ? $"{Format.Percent(drive.UsedPercent)} 空き少" : Format.Percent(drive.UsedPercent);

        ThroughputText = drive.ReadBytesPerSecond is null && drive.WriteBytesPerSecond is null
            ? Format.Missing
            : $"{Rate(drive.ReadBytesPerSecond)} / {Rate(drive.WriteBytesPerSecond)} MB/s";

        HealthText = HealthTextOf(drive);
        IsHealthCaution = drive.HealthStatus == DriveHealthStatus.Caution;
    }

    /// <summary>SSD は寿命（%）、HDD など寿命が取れないものは SMART の状態（良好 / 注意）。</summary>
    public static string HealthTextOf(DriveReading drive) => drive.HealthPercent is double health
        ? Format.Percent(health)
        : drive.HealthStatus switch
        {
            DriveHealthStatus.Good => "良好",
            DriveHealthStatus.Caution => "注意",
            _ => Format.Missing,
        };

    private static double? Scale(double? gb, bool terabytes) => terabytes ? gb / 1000 : gb;

    private static string Rate(double? bytesPerSecond) =>
        bytesPerSecond is double b ? Format.Number(b / 1_000_000, b < 10_000_000 ? "0.0" : "N0") : Format.Missing;
}

/// <summary>ストレージのタイル（2〜4 台のカード表示）。</summary>
public sealed class StorageDrivesViewModel : ObservableObject
{
    private string _subtitle = "";

    public string Title => "ストレージ";

    /// <summary>"4台"</summary>
    public string Subtitle { get => _subtitle; private set => SetProperty(ref _subtitle, value); }

    public ObservableCollection<DriveCardViewModel> Cards { get; } = [];

    public void Update(IReadOnlyList<DriveReading> drives, TemperatureThresholds thresholds)
    {
        Subtitle = $"{drives.Count}台";

        // 並びやディスクが変わったときだけカードを作り直し、それ以外は値だけ更新する
        if (!Cards.Select(c => c.Key).SequenceEqual(drives.Select(d => d.Key)))
        {
            Cards.Clear();
            foreach (var drive in drives)
                Cards.Add(new DriveCardViewModel(drive.Key));
        }

        for (int i = 0; i < drives.Count; i++)
            Cards[i].Update(drives[i], thresholds);
    }
}

/// <summary>設定画面で、表示するディスクを選ぶ行。</summary>
public sealed class DriveChoiceViewModel(string key, string title, Action<DriveChoiceViewModel, bool> onToggle) : ObservableObject
{
    private bool _isSelected;
    private bool _isEnabled = true;

    public string Key { get; } = key;

    public string Title { get; } = title;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value != _isSelected)
                onToggle(this, value);
            // 上限を超える選択は取り消されるため、常に現在値を通知し直す
            OnPropertyChanged();
        }
    }

    /// <summary>4 台を選んでいるときは、選んでいないディスクを選べなくする。</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public void SetSelected(bool selected)
    {
        if (_isSelected == selected)
            return;
        _isSelected = selected;
        OnPropertyChanged(nameof(IsSelected));
    }
}
