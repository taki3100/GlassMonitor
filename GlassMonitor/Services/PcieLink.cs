using System.Runtime.InteropServices;

namespace GlassMonitor.Services;

/// <summary>
/// NVMe ディスクの PCIe 世代。ディスクの親（PCIe コントローラー）の現在のリンク速度を PnP のプロパティストアから読む。
/// デバイスそのものには問い合わせない。
/// </summary>
internal static class PcieLink
{
    // DEVPKEY_PciDevice_CurrentLinkSpeed（1 = Gen1 … 6 = Gen6）
    private static readonly Guid PciDeviceProperties = new("3AB22E31-8264-4B4E-9AF5-A8D2D8E33E62");
    private const uint CurrentLinkSpeedId = 9;

    public static int? CurrentGeneration(string? diskPnpDeviceId)
    {
        if (string.IsNullOrEmpty(diskPnpDeviceId)
            || CM_Locate_DevNodeW(out uint device, diskPnpDeviceId, 0) != 0
            || CM_Get_Parent(out uint controller, device, 0) != 0)
        {
            return null;
        }

        var key = new DEVPROPKEY { FormatId = PciDeviceProperties, PropertyId = CurrentLinkSpeedId };
        uint size = sizeof(uint);
        if (CM_Get_DevNode_PropertyW(controller, ref key, out _, out uint speed, ref size, 0) != 0)
            return null;
        return speed is >= 1 and <= 6 ? (int)speed : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVPROPKEY
    {
        public Guid FormatId;
        public uint PropertyId;
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Locate_DevNodeW(out uint devInst, string deviceId, int flags);

    [DllImport("cfgmgr32.dll")]
    private static extern int CM_Get_Parent(out uint parent, uint devInst, int flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern int CM_Get_DevNode_PropertyW(
        uint devInst, ref DEVPROPKEY key, out uint propertyType, out uint buffer, ref uint bufferSize, int flags);
}
