using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using GlassMonitor.Settings;
using static GlassMonitor.Interop.NativeMethods;

namespace GlassMonitor.Interop;

/// <summary>
/// GetWindowPlacement / SetWindowPlacement でウィンドウ位置を保存・復元する。
/// 物理ピクセルで扱うため DPI の異なるモニター間でもずれにくく、
/// 保存先モニターが無くなっていれば OS が画面内に補正してくれる。
/// </summary>
internal static class WindowPlacement
{
    public static WindowBounds? Capture(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var placement = new WINDOWPLACEMENT { Length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (hwnd == IntPtr.Zero || !GetWindowPlacement(hwnd, ref placement))
            return null;

        var rc = placement.NormalPosition;
        bool maximized = placement.ShowCmd == SW_SHOWMAXIMIZED
            || (placement.ShowCmd == SW_SHOWMINIMIZED && (placement.Flags & WPF_RESTORETOMAXIMIZED) != 0);
        return new WindowBounds(rc.Left, rc.Top, rc.Right, rc.Bottom, maximized);
    }

    public static void Restore(Window window, WindowBounds? bounds)
    {
        if (bounds is null || bounds.Right <= bounds.Left || bounds.Bottom <= bounds.Top)
            return;

        var hwnd = new WindowInteropHelper(window).Handle;
        var placement = new WINDOWPLACEMENT
        {
            Length = Marshal.SizeOf<WINDOWPLACEMENT>(),
            ShowCmd = bounds.Maximized ? SW_SHOWMAXIMIZED : SW_SHOWNORMAL,
            NormalPosition = new RECT { Left = bounds.Left, Top = bounds.Top, Right = bounds.Right, Bottom = bounds.Bottom },
        };
        SetWindowPlacement(hwnd, ref placement);
    }

    /// <summary>
    /// ウィンドウがモニターの作業領域からはみ出していれば内側に寄せる。
    /// 保存時より最小サイズが大きくなった場合や、解像度が変わった場合に備える。
    /// </summary>
    public static void EnsureWithinWorkArea(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || window.WindowState != WindowState.Normal || !GetWindowRect(hwnd, out var rect))
            return;

        var info = new MONITORINFO { Size = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref info))
            return;

        var work = info.Work;
        int width = Math.Min(rect.Right - rect.Left, work.Right - work.Left);
        int height = Math.Min(rect.Bottom - rect.Top, work.Bottom - work.Top);
        int left = Math.Clamp(rect.Left, work.Left, work.Right - width);
        int top = Math.Clamp(rect.Top, work.Top, work.Bottom - height);

        if (left != rect.Left || top != rect.Top || width != rect.Right - rect.Left || height != rect.Bottom - rect.Top)
            SetWindowPos(hwnd, IntPtr.Zero, left, top, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
    }
}
