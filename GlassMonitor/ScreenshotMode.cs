using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GlassMonitor.Interop;
using GlassMonitor.Settings;
using GlassMonitor.ViewModels;

namespace GlassMonitor;

/// <summary>
/// --screenshot: README 用のスクリーンショットを撮る。既定の設定・既定のサイズで画面中央に表示し、
/// グラフが 60 秒分たまったところで表示の更新を止めて、ウィンドウを PNG に保存して終了する。
/// </summary>
internal sealed class ScreenshotMode(Window window, string path)
{
    // リングのアニメーションと DWM の合成が落ち着くまで待つ
    private static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(1.5);

    private int _snapshots;

    /// <summary>撮影の準備に入ったら true。以降のセンサーの値は表示に反映しない。</summary>
    public bool IsCapturing { get; private set; }

    /// <summary>
    /// 撮影用の設定。利用者の設定は読み込まず、保存もしない（ウィンドウ位置やアクセント色を書き換えないため）。
    /// 起動時の PawnIO の案内も出さない。
    /// </summary>
    public static AppSettings CreateSettings()
    {
        SettingsStore.IsReadOnly = true;
        return new AppSettings { ShowPawnIoGuideOnStartup = false };
    }

    /// <summary>ウィンドウを表示したあとに呼ぶ。他のウィンドウに隠れないよう最前面にする（設定の「常に最前面」とは別）。</summary>
    public void Start()
    {
        window.Topmost = true;
        window.Activate();
    }

    public void OnSnapshotApplied()
    {
        if (IsCapturing || ++_snapshots < TileViewModel.HistoryLength)
            return;

        IsCapturing = true;
        var timer = new DispatcherTimer { Interval = SettleTime };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Capture();
        };
        timer.Start();
    }

    private void Capture()
    {
        int exitCode = 0;
        try
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(ScreenCapture.CaptureWindow(window)));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = File.Create(path);
            encoder.Save(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
        {
            MessageBox.Show(ex.Message, "GlassMonitor", MessageBoxButton.OK, MessageBoxImage.Error);
            exitCode = 1;
        }
        Application.Current.Shutdown(exitCode);
    }
}
