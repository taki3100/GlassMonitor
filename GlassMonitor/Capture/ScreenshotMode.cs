using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GlassMonitor.Interop;

namespace GlassMonitor.Capture;

/// <summary>
/// --screenshot: README 用のスクリーンショットを撮る。グラフが 60 秒分たまったところで表示の更新を止めて、
/// 画面に表示されたウィンドウ（Acrylic の背景を含む）を PNG に保存して終了する。
/// </summary>
internal sealed class ScreenshotMode(Window window, string path) : CaptureMode
{
    // リングのアニメーションと DWM の合成が落ち着くまで待つ
    private static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(1.5);

    /// <summary>画面から切り出すので、他のウィンドウに隠れないよう最前面にする（設定の「常に最前面」とは別）。</summary>
    public override void Start()
    {
        window.Topmost = true;
        window.Activate();
    }

    protected override void Begin()
    {
        ShowsSnapshots = false;
        var timer = new DispatcherTimer { Interval = SettleTime };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Exit(Capture());
        };
        timer.Start();
    }

    private Exception? Capture()
    {
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(ScreenCapture.CaptureWindow(window)));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = File.Create(path);
            encoder.Save(stream);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
        {
            return ex;
        }
    }
}
