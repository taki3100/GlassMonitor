using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using static GlassMonitor.Interop.NativeMethods;

namespace GlassMonitor.Interop;

/// <summary>DWM のシステムバックドロップ（Acrylic）と角丸をウィンドウに適用する。</summary>
internal static class WindowBackdrop
{
    // DWMWA_SYSTEMBACKDROP_TYPE は Windows 11 22H2 (build 22621) 以降
    private static bool IsSupported => Environment.OSVersion.Version.Build >= 22621;

    /// <returns>Acrylic を適用できた場合 true。</returns>
    public static bool TryApplyAcrylic(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
            return false;

        int darkMode = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

        int corner = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

        // 枠線は白 25% 程度に見える落ち着いた色（COLORREF = 0x00BBGGRR）
        int border = 0x00_70_60_5C;
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));

        if (!IsSupported)
            return false;

        // WPF の描画面を透明にしてバックドロップを透過させる
        if (HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } target)
            target.BackgroundColor = Colors.Transparent;

        var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);

        int backdrop = DWMSBT_TRANSIENTWINDOW;
        if (DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int)) != 0)
            return false;

        HwndSource.FromHwnd(hwnd)?.AddHook(KeepBackdropActive);
        return true;
    }

    /// <summary>
    /// 非アクティブになると DWM は Acrylic を単色に切り替えてしまう。
    /// 常に前面に置いて眺める用途なので、非クライアント領域を常に「アクティブ」として処理させて Acrylic を維持する。
    /// （キーボードフォーカスや Window.IsActive は WM_ACTIVATE で決まるため影響しない）
    /// </summary>
    private static IntPtr KeepBackdropActive(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_NCACTIVATE || wParam != IntPtr.Zero)
            return IntPtr.Zero;

        handled = true;
        // lParam = -1 は非クライアント領域の再描画を抑止する（WindowChrome と同じ扱い）
        return DefWindowProc(hwnd, WM_NCACTIVATE, new IntPtr(1), new IntPtr(-1));
    }
}
