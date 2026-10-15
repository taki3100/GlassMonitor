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
/// Acrylic の背景（すりガラス越しのデスクトップ）は DWM が合成するため写らない。代わりに、色の玉をぼかしたような
/// 背景をウィンドウの背後に敷き、ウィンドウの半透明の色味越しに透けて見えるようにする（録画のときだけ）。
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

    // ウィンドウの背後に敷く背景: 濃紺の地に、ぼかした色の玉（位置と半径はウィンドウの幅・高さに対する割合）
    private static readonly Color BackdropBase = Color.FromRgb(0x12, 0x12, 0x2E);
    private static readonly (double X, double Y, double Radius, Color Color)[] BackdropBlobs =
    [
        (0.10, 0.14, 0.42, Color.FromRgb(0xFF, 0x6A, 0x3D)), // 左上: オレンジ
        (0.70, -0.04, 0.26, Color.FromRgb(0xFF, 0x5C, 0xA8)), // 右上: ピンク
        (1.02, 0.46, 0.40, Color.FromRgb(0x3D, 0x6B, 0xFF)), // 右: 青
        (0.04, 0.84, 0.34, Color.FromRgb(0x22, 0xC3, 0xD6)), // 左下: ティール
        (0.55, 1.06, 0.40, Color.FromRgb(0x9B, 0x3D, 0xFF)), // 下: 紫
    ];

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
        _background = CreateBackdrop(new Size(window.ActualWidth, window.ActualHeight));

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

    /// <summary>
    /// ウィンドウの背後に敷く、色の玉をぼかしたような背景。玉は中心から外側へ透明になる放射グラデーションで描く。
    /// この上にウィンドウ（半透明の色味・グロー・ガラスのタイル）を重ねると、すりガラス越しに色が透けて見える。
    /// </summary>
    private static DrawingVisual CreateBackdrop(Size size)
    {
        var visual = new DrawingVisual();
        using var dc = visual.RenderOpen();
        dc.DrawRectangle(new SolidColorBrush(BackdropBase), null, new Rect(size));
        foreach (var (x, y, radius, color) in BackdropBlobs)
        {
            var brush = new RadialGradientBrush(
            [
                new GradientStop(Color.FromArgb(0xF0, color.R, color.G, color.B), 0),
                new GradientStop(Color.FromArgb(0x80, color.R, color.G, color.B), 0.45),
                new GradientStop(Color.FromArgb(0x00, color.R, color.G, color.B), 1),
            ]);
            double r = radius * size.Width;
            dc.DrawEllipse(brush, null, new Point(x * size.Width, y * size.Height), r, r);
        }
        return visual;
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
