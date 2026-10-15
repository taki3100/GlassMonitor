namespace GlassMonitor.Services;

/// <summary>
/// --demo-disks N: 実際のディスクの代わりに表示するダミーのディスク（2〜5 台）。
/// ディスクが 1 台の PC でも、カード表示（2〜4 台）やドライブの選択（5 台）を確認できるようにする。
/// 空き容量が少ない、温度が高め、温度が取れない、ドライブ文字が無い、SMART が「注意」のディスクを含める。
/// </summary>
internal sealed class DemoDrives
{
    public const int MinCount = 2;
    public const int MaxCount = 5;

    private static readonly DemoDisk[] Disks =
    [
        new("C:", "System", "WD_BLACK SN8100", "NVMe Gen5", 2000, 0.47, 44, 100, DriveHealthStatus.Good, 1500),
        new("D:", "Data", "Samsung SSD 990 PRO", "NVMe Gen4", 4000, 0.62, 63, 98, DriveHealthStatus.Good, 1200),
        new("E:", "Games", "Crucial MX500", "SATA SSD", 1000, 0.93, 36, 87, DriveHealthStatus.Good, 400),
        new("F:", "Backup", "WDC WD40EZAZ", "HDD", 4000, 0.71, null, null, DriveHealthStatus.Good, 150),
        new(null, null, "ST8000VN004", "HDD", 8000, 0.25, 51, null, DriveHealthStatus.Caution, 150),
    ];

    private readonly DemoDisk[] _disks;
    private readonly DateTime _start = DateTime.Now;
    private readonly Random _random = new(1);

    /// <param name="count">表示する台数。2〜5 に収める。</param>
    public DemoDrives(int count) => _disks = Disks[..Math.Clamp(count, MinCount, MaxCount)];

    public (IReadOnlyList<DriveReading> Shown, IReadOnlyList<DriveChoice> All) Read(IReadOnlyCollection<string> selectedKeys)
    {
        double seconds = (DateTime.Now - _start).TotalSeconds;
        var shown = DriveReader.Choose(_disks, d => d.Key, selectedKeys)
            .Select(d => d.Reading(Array.IndexOf(_disks, d), seconds, _random))
            .ToList();
        var all = _disks.Select((d, i) => new DriveChoice(d.Key, d.Letters, i, d.Model)).ToList();
        return (shown, all);
    }

    /// <param name="MaxMbps">読み書き速度の目安（MB/s）。グラフが動くように、時間とともに変化させる。</param>
    private sealed record DemoDisk(
        string? Letters,
        string? Label,
        string Model,
        string Kind,
        double TotalGb,
        double UsedRatio,
        double? Temperature,
        double? HealthPercent,
        DriveHealthStatus HealthStatus,
        double MaxMbps)
    {
        public string Key => $"demo|{Model}";

        public DriveReading Reading(int driveNumber, double seconds, Random random)
        {
            // ゆっくりした波に、ときどき大きな読み書きを重ねる
            double wave = (1 + Math.Sin(seconds * 0.4 + driveNumber * 1.7)) / 2;
            double burst = random.NextDouble() < 0.08 ? random.NextDouble() : 0;
            double read = MaxMbps * Math.Min(1, 0.05 * wave + burst) * 1_000_000;
            double write = MaxMbps * Math.Min(1, 0.03 * (1 - wave) + burst * 0.4) * 1_000_000;
            double activity = Math.Min(100, (read + write) / (MaxMbps * 1_000_000) * 100);

            return new DriveReading(
                Key,
                driveNumber,
                Model,
                Kind,
                Letters,
                Label,
                Temperature,
                TotalGb,
                TotalGb * (1 - UsedRatio),
                HealthPercent,
                HealthStatus,
                read,
                write,
                activity);
        }
    }
}
