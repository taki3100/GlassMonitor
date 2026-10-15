namespace GlassMonitor.ViewModels;

/// <summary>ストレージのタイル。使用容量・温度に加えて、読込/書込速度の 2 本線グラフを持つ。</summary>
public sealed class StorageTileViewModel : TileViewModel
{
    private const double BytesPerMegabyte = 1_000_000;

    // アイドル時のわずかな I/O でグラフが振り切れないよう、縦軸は最低でもこの値（MB/s）にする
    private const double MinimumScale = 10;

    private readonly double?[] _read = new double?[HistoryLength];
    private readonly double?[] _write = new double?[HistoryLength];

    private string _readText = Format.Missing;
    private string _writeText = Format.Missing;
    private string _activityText = Format.Missing;
    private IReadOnlyList<double?> _readHistory;
    private IReadOnlyList<double?> _writeHistory;
    private double _graphMaximum = MinimumScale;

    public StorageTileViewModel()
        : base("Tile_Storage", "Tile_Temperature", "Tile_StorageUsed", "Tile_Free", "Tile_Health")
    {
        UsageLabelKey = "Tile_UsedSpace";
        _readHistory = (double?[])_read.Clone();
        _writeHistory = (double?[])_write.Clone();
    }

    public string ReadText
    {
        get => _readText;
        private set => SetProperty(ref _readText, value);
    }

    public string WriteText
    {
        get => _writeText;
        private set => SetProperty(ref _writeText, value);
    }

    /// <summary>アクティブ時間（%）。</summary>
    public string ActivityText
    {
        get => _activityText;
        private set => SetProperty(ref _activityText, value);
    }

    /// <summary>直近 60 秒の読込速度（MB/s）。</summary>
    public IReadOnlyList<double?> ReadHistory
    {
        get => _readHistory;
        private set => SetProperty(ref _readHistory, value);
    }

    /// <summary>直近 60 秒の書込速度（MB/s）。</summary>
    public IReadOnlyList<double?> WriteHistory
    {
        get => _writeHistory;
        private set => SetProperty(ref _writeHistory, value);
    }

    /// <summary>2 本の線で共有する縦軸の上限（MB/s）。</summary>
    public double GraphMaximum
    {
        get => _graphMaximum;
        private set => SetProperty(ref _graphMaximum, value);
    }

    public void PushThroughput(double? readBytesPerSecond, double? writeBytesPerSecond, double? activityPercent)
    {
        double? read = readBytesPerSecond / BytesPerMegabyte;
        double? write = writeBytesPerSecond / BytesPerMegabyte;

        // 縦軸は、押し出される最古の値を除いた直近 60 秒の最大値に合わせる
        double peak = _read.Skip(1).Concat(_write.Skip(1)).Append(read).Append(write).Max(v => v ?? 0);
        GraphMaximum = NiceCeiling(Math.Max(peak * 1.1, MinimumScale));

        ReadHistory = Push(_read, read);
        WriteHistory = Push(_write, write);
        ReadText = Format.MegabytesPerSecond(read);
        WriteText = Format.MegabytesPerSecond(write);
        ActivityText = Format.Percent(activityPercent is double a ? Math.Clamp(a, 0, 100) : null);
    }

    /// <summary>1・2・5 系列のきりのよい値に切り上げる。</summary>
    private static double NiceCeiling(double value)
    {
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(value)));
        ReadOnlySpan<double> steps = [1, 2, 5, 10];
        foreach (double step in steps)
        {
            if (step * magnitude >= value)
                return step * magnitude;
        }
        return 10 * magnitude;
    }
}
