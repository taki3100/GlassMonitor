using System.IO;
using DiskInfoToolkit.Enums.Interop;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Storage;

namespace GlassMonitor.Services;

/// <summary>
/// 物理ディスクごとの情報を読む。表示するのは最大 4 台で、5 台以上あるときは設定で選んだものを表示する。
/// SMART などの読み取り（StorageDevice.Update）は表示するディスクだけに行い、Corsair 製のディスクは扱わない。
/// </summary>
internal sealed class DriveReader
{
    public const int MaxDisplayed = 4;

    private const double BytesPerGb = 1_000_000_000;

    // PCIe の世代は変わらないので、ディスクごとに一度だけ調べる
    private readonly Dictionary<StorageDevice, int?> _pcieGeneration = [];

    public (IReadOnlyList<DriveReading> Shown, IReadOnlyList<DriveChoice> All) Read(
        IEnumerable<IHardware> hardware, IReadOnlyCollection<string> selectedKeys)
    {
        // ドライブ文字の順（C: D: …）。文字の無いディスクは最後
        var drives = hardware.OfType<StorageDevice>()
            .Where(d => !IsCorsair(d.Storage))
            .Select(d => new Drive(d, KeyOf(d.Storage), LettersOf(d.Storage)))
            .OrderBy(d => d.Letters.Count == 0 ? 1 : 0)
            .ThenBy(d => d.Letters.Count > 0 ? d.Letters[0] : char.MaxValue)
            .ThenBy(d => d.Device.Storage.DriveNumber)
            .ToList();

        var readings = new List<DriveReading>();
        foreach (var drive in Choose(drives, d => d.Key, selectedKeys))
        {
            drive.Device.Update();
            readings.Add(ReadDrive(drive));
        }

        var choices = drives
            .Select(d => new DriveChoice(d.Key, d.LettersText, d.Device.Storage.DriveNumber, NameFormatter.Storage(d.Device.Storage.Model)))
            .ToList();
        return (readings, choices);
    }

    private DriveReading ReadDrive(Drive drive)
    {
        var device = drive.Device;
        var storage = device.Storage;

        // 容量はドライブ文字のあるボリュームの合計。文字が無いときは LibreHardwareMonitor の値を使う
        var (total, free, label) = ReadVolumes(drive.Letters);
        if (total is null)
        {
            total = Value(Find(device, SensorType.Data, "Total Space"));
            free = Value(Find(device, SensorType.Data, "Free Space"));
        }

        // 健康状態 = 100 - Percentage Used（NVMe）。無ければ SMART から求めた寿命。HDD は寿命が無いので状態だけ
        double? percentageUsed = Value(Find(device, SensorType.Level, "Percentage Used"));
        double? health = percentageUsed is double used
            ? Math.Clamp(100 - used, 0, 100)
            : storage.Smart.Life is sbyte life && life >= 0 ? life : null;

        var status = storage.Smart.DiskStatus switch
        {
            DiskStatus.Good => DriveHealthStatus.Good,
            DiskStatus.Caution or DiskStatus.Bad => DriveHealthStatus.Caution,
            _ => DriveHealthStatus.Unknown,
        };

        var temperature = Find(device, SensorType.Temperature, "Composite Temperature", "Temperature")
            ?? device.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && !IsTemperatureLimit(s));

        return new DriveReading(
            drive.Key,
            storage.DriveNumber,
            NameFormatter.Storage(storage.Model),
            KindOf(device),
            drive.LettersText,
            label,
            Value(temperature),
            total,
            free,
            health,
            status,
            Value(Find(device, SensorType.Throughput, "Read Rate")),
            Value(Find(device, SensorType.Throughput, "Write Rate")),
            Value(Find(device, SensorType.Load, "Total Activity")));
    }

    /// <summary>"NVMe Gen5" / "SATA SSD" / "HDD" / "USB SSD" など。</summary>
    private string KindOf(StorageDevice device)
    {
        var storage = device.Storage;
        if (storage.BusType == StorageBusType.BusTypeUsb)
            return storage.IsSSD ? "USB SSD" : "USB";

        if (storage.IsNVMe)
        {
            if (!_pcieGeneration.TryGetValue(device, out int? generation))
                _pcieGeneration[device] = generation = PcieLink.CurrentGeneration(storage.DeviceID);
            return generation is int g ? $"NVMe Gen{g}" : "NVMe";
        }

        return storage.IsSSD ? "SATA SSD" : "HDD";
    }

    /// <summary>表示するディスクを選ぶ。4 台以下なら全部、5 台以上なら設定で選んだもの（無ければ先頭の 4 台）。</summary>
    internal static IEnumerable<T> Choose<T>(IReadOnlyList<T> drives, Func<T, string> keyOf, IReadOnlyCollection<string> selectedKeys)
    {
        if (drives.Count <= MaxDisplayed)
            return drives;

        var selected = drives.Where(d => selectedKeys.Contains(keyOf(d))).Take(MaxDisplayed).ToList();
        return selected.Count > 0 ? selected : drives.Take(MaxDisplayed);
    }

    private static (double? TotalGb, double? FreeGb, string? Label) ReadVolumes(IReadOnlyList<char> letters)
    {
        double total = 0;
        double free = 0;
        bool any = false;
        var labels = new List<string>();

        foreach (char letter in letters)
        {
            try
            {
                var volume = new DriveInfo($"{letter}:\\");
                if (!volume.IsReady)
                    continue;

                total += volume.TotalSize;
                free += volume.TotalFreeSpace;
                any = true;
                if (!string.IsNullOrWhiteSpace(volume.VolumeLabel))
                    labels.Add(volume.VolumeLabel.Trim());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // 取り外し中などで読めないボリュームは飛ばす
            }
        }

        if (!any)
            return (null, null, null);
        return (total / BytesPerGb, free / BytesPerGb, labels.Count > 0 ? string.Join(" / ", labels.Distinct()) : null);
    }

    private static IReadOnlyList<char> LettersOf(DiskInfoToolkit.Storage storage) => storage.Partitions
        .Where(p => p.DriveLetter.HasValue)
        .Select(p => char.ToUpperInvariant(p.DriveLetter!.Value))
        .Distinct()
        .Order()
        .ToList();

    /// <summary>設定に保存するキー。シリアル番号が無いディスクは型番とディスク番号で区別する。</summary>
    private static string KeyOf(DiskInfoToolkit.Storage storage) =>
        string.IsNullOrWhiteSpace(storage.SerialNumber)
            ? $"{storage.Model?.Trim()}#{storage.DriveNumber}"
            : $"{storage.Model?.Trim()}|{storage.SerialNumber.Trim()}";

    private static bool IsCorsair(DiskInfoToolkit.Storage storage) =>
        (storage.Model?.Contains("Corsair", StringComparison.OrdinalIgnoreCase) ?? false)
        || (storage.Vendor?.Contains("Corsair", StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool IsTemperatureLimit(ISensor s) =>
        s.Name.Contains("Warning", StringComparison.OrdinalIgnoreCase)
        || s.Name.Contains("Critical", StringComparison.OrdinalIgnoreCase)
        || s.Name.Contains("Limit", StringComparison.OrdinalIgnoreCase);

    private static ISensor? Find(IHardware hardware, SensorType type, params string[] names)
    {
        foreach (var name in names)
        {
            var match = hardware.Sensors.FirstOrDefault(s =>
                s.SensorType == type && s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
        }
        return null;
    }

    private static double? Value(ISensor? sensor) =>
        sensor?.Value is float v && float.IsFinite(v) ? v : null;

    private sealed record Drive(StorageDevice Device, string Key, IReadOnlyList<char> Letters)
    {
        /// <summary>"C: D:"（ドライブ文字が無ければ null）。</summary>
        public string? LettersText => Letters.Count > 0 ? string.Join(" ", Letters.Select(c => $"{c}:")) : null;
    }
}
