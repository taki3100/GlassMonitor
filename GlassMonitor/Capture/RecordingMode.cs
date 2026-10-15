using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace GlassMonitor.Capture;

/// <summary>
/// --record &lt;秒数&gt; &lt;file.gif&gt;: README 用のデモ GIF を撮る。グラフが 60 秒分たまったところから、
/// 表示を更新し続けたままウィンドウの描画内容を連続で撮影し、ループ再生する GIF に保存して終了する。
/// </summary>
/// <remarks>
/// 画面からは切り出さず WPF の描画内容だけを撮るので、デスクトップや他のウィンドウは写らない。
/// Acrylic の背景は DWM が合成するため写らず、代わりに Acrylic が使えないときと同じ単色の背景を敷く。
/// </remarks>
internal sealed class RecordingMode(Window window, TimeSpan duration, string path) : CaptureMode
{
    // 横幅 900 ピクセル前後、15 fps 程度
    private const double TargetWidth = 900;
    private static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(1.0 / 15);

    // DispatcherTimer は処理が終わってから次の間隔を数えるので、短い間隔で時計を見て予定の時刻に撮る
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(5);

    // 録画の 2〜6 秒目に、CPU のグラフと使用率が動いて見えるよう軽い負荷をかける
    private static readonly TimeSpan LoadStart = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LoadLength = TimeSpan.FromSeconds(4);

    private readonly BlockingCollection<(byte[] Pixels, TimeSpan Time)> _frames = new();
    private readonly Stopwatch _clock = new();
    private Task? _encoding;
    private DispatcherTimer? _timer;
    private TimeSpan _nextFrame;
    private RenderTargetBitmap? _target;
    private DrawingVisual? _background;

    /// <summary>マウスが乗ったときの表示（ボタンのハイライトなど）が写らないようにする。</summary>
    public override void Start() => window.IsHitTestVisible = false;

    protected override void Begin()
    {
        double scale = TargetWidth / window.ActualWidth;
        int width = (int)Math.Round(window.ActualWidth * scale);
        int height = (int)Math.Round(window.ActualHeight * scale);
        _target = new RenderTargetBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        _background = new DrawingVisual();
        using (var dc = _background.RenderOpen())
        {
            dc.DrawRectangle((Brush)window.FindResource("FallbackWindowBrush"), null,
                new Rect(0, 0, window.ActualWidth, window.ActualHeight));
        }

        // GIF への変換は別スレッドで行い、撮影（UI スレッド）を止めない
        _encoding = Task.Run(() => Encode(width, height));
        _encoding.ContinueWith(t => Exit(t.Exception?.GetBaseException()), TaskScheduler.FromCurrentSynchronizationContext());

        StartCpuLoad();
        _clock.Start();
        // 優先度を Background にして、センサーの値の反映と描画が済んでから撮る
        _timer = new DispatcherTimer(PollInterval, DispatcherPriority.Background, (_, _) => Tick(), window.Dispatcher);
        Tick();
    }

    private void Tick()
    {
        var time = _clock.Elapsed;
        // 書き出しに失敗したときは撮影もやめる（終了とエラーの表示は _encoding の続きで行う）
        if (time >= duration || _encoding!.IsCompleted)
        {
            _timer!.Stop();
            _frames.CompleteAdding();
            return;
        }

        // 予定の時刻（1/15 秒ごと）を過ぎたら撮る。撮れなかった分は詰めずに飛ばす
        if (time < _nextFrame)
            return;
        while (_nextFrame <= time)
            _nextFrame += FrameInterval;

        _target!.Clear();
        _target.Render(_background);
        _target.Render(window);
        var pixels = new byte[_target.PixelWidth * _target.PixelHeight * 4];
        _target.CopyPixels(pixels, _target.PixelWidth * 4, 0);
        _frames.Add((pixels, time));
    }

    private void Encode(int width, int height)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new BufferedStream(File.Create(path));
        var encoder = new GifEncoder(stream, width, height);
        foreach (var (pixels, time) in _frames.GetConsumingEnumerable())
            encoder.AddFrame(pixels, time);
        encoder.Finish(duration);
    }

    /// <summary>論理プロセッサーの 1/4 のスレッドで、優先度を下げて数秒だけ計算を回す。</summary>
    private static void StartCpuLoad()
    {
        int threads = Math.Max(1, Environment.ProcessorCount / 4);
        for (int i = 0; i < threads; i++)
        {
            new Thread(() =>
            {
                Thread.Sleep(LoadStart);
                var clock = Stopwatch.StartNew();
                double x = 1;
                while (clock.Elapsed < LoadLength)
                    x = Math.Sqrt(x + 1);
                GC.KeepAlive(x);
            })
            { IsBackground = true, Priority = ThreadPriority.BelowNormal }.Start();
        }
    }
}
