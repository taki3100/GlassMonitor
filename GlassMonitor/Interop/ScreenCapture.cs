using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static GlassMonitor.Interop.NativeMethods;

namespace GlassMonitor.Interop;

/// <summary>
/// 画面に表示されているウィンドウをそのまま撮る。Acrylic の背景は DWM が合成するため、
/// WPF の描画（RenderTargetBitmap）ではなく、合成後の画面からウィンドウの範囲を切り出す。
/// </summary>
internal static class ScreenCapture
{
    public static BitmapSource CaptureWindow(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (!GetWindowRect(hwnd, out var rect))
            throw new Win32Exception();

        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;
        IntPtr screen = GetDC(IntPtr.Zero);
        IntPtr memory = CreateCompatibleDC(screen);
        IntPtr bitmap = CreateCompatibleBitmap(screen, width, height);
        IntPtr previous = SelectObject(memory, bitmap);
        try
        {
            if (!BitBlt(memory, 0, 0, width, height, screen, rect.Left, rect.Top, SRCCOPY | CAPTUREBLT))
                throw new Win32Exception();
            SelectObject(memory, previous);

            var source = Imaging.CreateBitmapSourceFromHBitmap(
                bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            // 画面のピクセルにアルファは無いので、透明な PNG にならないよう不透明の形式にする
            var opaque = new FormatConvertedBitmap(source, PixelFormats.Bgr32, null, 0);
            opaque.Freeze();
            return opaque;
        }
        finally
        {
            SelectObject(memory, previous);
            DeleteObject(bitmap);
            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }
}
