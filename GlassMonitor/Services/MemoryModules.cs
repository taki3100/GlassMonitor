using System.Management;

namespace GlassMonitor.Services;

/// <summary>
/// 搭載メモリの容量・規格・動作速度。LibreHardwareMonitor には実効速度のセンサーが無いため WMI から取得する。
/// </summary>
internal readonly record struct MemoryModules(double? CapacityGb, string? Type, int? SpeedMts)
{
    public static MemoryModules Query()
    {
        try
        {
            ulong capacity = 0;
            int? speed = null;
            string? type = null;

            using var searcher = new ManagementObjectSearcher(
                "SELECT Capacity, Speed, ConfiguredClockSpeed, SMBIOSMemoryType FROM Win32_PhysicalMemory");
            foreach (var module in searcher.Get().Cast<ManagementObject>())
            {
                using (module)
                {
                    capacity += Convert.ToUInt64(module["Capacity"] ?? 0UL);

                    int configured = Convert.ToInt32(module["ConfiguredClockSpeed"] ?? 0);
                    int rated = Convert.ToInt32(module["Speed"] ?? 0);
                    int current = configured > 0 ? configured : rated;
                    if (current > 0)
                        speed = speed is null ? current : Math.Min(speed.Value, current);

                    type ??= TypeName(Convert.ToInt32(module["SMBIOSMemoryType"] ?? 0));
                }
            }

            return new MemoryModules(capacity > 0 ? capacity / (1024.0 * 1024 * 1024) : null, type, speed);
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            return default;
        }
    }

    // SMBIOS Type 17 Memory Type
    private static string? TypeName(int smbiosType) => smbiosType switch
    {
        24 => "DDR3",
        26 => "DDR4",
        29 => "LPDDR3",
        30 => "LPDDR4",
        34 => "DDR5",
        35 => "LPDDR5",
        _ => null,
    };
}
