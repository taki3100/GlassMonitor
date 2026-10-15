using System.Runtime.InteropServices;

namespace GlassMonitor.Services;

/// <summary>
/// 物理メモリの使用状況。LibreHardwareMonitor の Total Memory と同じ GlobalMemoryStatusEx から取得する。
/// メモリ温度が OFF のときは IsMemoryEnabled を無効にするため、使用量は常にこちらから読む。
/// </summary>
internal static class PhysicalMemoryStatus
{
    private const double BytesPerGb = 1024.0 * 1024 * 1024;

    public static (double LoadPercent, double UsedGb, double AvailableGb, double TotalGb)? Query()
    {
        var status = new MEMORYSTATUSEX { Length = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0)
            return null;

        double used = status.TotalPhys - status.AvailPhys;
        return (used / status.TotalPhys * 100, used / BytesPerGb, status.AvailPhys / BytesPerGb, status.TotalPhys / BytesPerGb);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);
}
