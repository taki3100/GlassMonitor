using System.Security.Principal;

namespace GlassMonitor.Services;

internal static class SystemStatus
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// インストール済みの PawnIO のバージョン（未インストールなら null）。
    /// LibreHardwareMonitor と同じく、アンインストール情報のレジストリから読む。
    /// インストールされていても、ドライバーがブロックされて使えない場合がある（CPU センサーの値で判断する）。
    /// </summary>
    public static Version? PawnIoVersion => LibreHardwareMonitor.PawnIo.PawnIo.Version;
}
