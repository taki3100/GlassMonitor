namespace GlassMonitor.Services;

// センサーが取得できない値は null。

public sealed record CpuReading(
    string? Name,
    double? Load,
    double? Temperature,
    double? ClockMhz,
    double? PowerWatts,
    double? FanRpm);

public sealed record GpuReading(
    string? Name,
    double? Load,
    double? Temperature,
    double? ClockMhz,
    double? PowerWatts,
    double? FanRpm,
    double? MemoryTotalMb,
    double? MemoryUsedMb,
    double? MemoryFreeMb,
    double? MemoryClockMhz,
    double? MemoryControllerLoad)
{
    public static GpuReading Empty { get; } = new(null, null, null, null, null, null, null, null, null, null, null);
}

/// <summary>使用量は GlobalMemoryStatusEx（GiB）、速度・規格・搭載容量は起動時の WMI、温度は SPD（ON のときのみ）。</summary>
public sealed record MemoryReading(
    double? Load,
    double? UsedGb,
    double? AvailableGb,
    double? TotalGb,
    double? Temperature,
    double? InstalledGb,
    string? Type,
    int? SpeedMts);

/// <summary>SMART による正常性（HDD など寿命 % が無いドライブの健康状態に使う）。</summary>
public enum DriveHealthStatus
{
    Unknown,
    Good,
    Caution,
}

/// <summary>
/// 物理ディスク 1 台分。容量はドライブ文字のあるボリュームの合計（10 進の GB）、速度は バイト/秒。
/// </summary>
public sealed record DriveReading(
    string Key,
    int DriveNumber,
    string? Model,
    string? Kind,
    string? Letters,
    string? Label,
    double? Temperature,
    double? TotalGb,
    double? FreeGb,
    double? HealthPercent,
    DriveHealthStatus HealthStatus,
    double? ReadBytesPerSecond,
    double? WriteBytesPerSecond,
    double? ActivityPercent)
{
    public double? UsedGb => TotalGb - FreeGb;

    public double? UsedPercent => TotalGb > 0 ? UsedGb / TotalGb * 100 : null;

    public double? FreePercent => TotalGb > 0 ? FreeGb / TotalGb * 100 : null;
}

/// <summary>設定画面で表示するドライブを選ぶための一覧の 1 行。Letters はドライブ文字（"C: D:"、無ければ null）。</summary>
public sealed record DriveChoice(string Key, string? Letters, int DriveNumber, string? Model);

public sealed record HardwareSnapshot(
    CpuReading Cpu,
    GpuReading Gpu,
    MemoryReading Memory,
    IReadOnlyList<DriveReading> Drives,
    IReadOnlyList<DriveChoice> AvailableDrives,
    DateTime Timestamp);
