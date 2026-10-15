using System.IO;

namespace GlassMonitor;

/// <summary>
/// 起動オプション。
/// <list type="bullet">
/// <item><c>--lang &lt;auto|ja|en …&gt;</c>: この起動だけ表示言語を切り替える（設定は変えない）。</item>
/// <item><c>--screenshot &lt;file.png&gt;</c>: README 用のスクリーンショットを撮って終了する（Capture.ScreenshotMode）。</item>
/// <item><c>--record &lt;秒数&gt; &lt;file.gif&gt;</c>: README 用のデモ GIF を撮って終了する（Capture.RecordingMode）。</item>
/// <item>
/// <c>--demo-disks &lt;2〜5&gt;</c>: 実際のディスクの代わりにダミーのディスクを表示する（カード表示やドライブの選択の確認用）。
/// この起動では設定を保存しない。
/// </item>
/// </list>
/// </summary>
internal sealed record StartupOptions(
    string? Language,
    string? ScreenshotPath,
    TimeSpan RecordDuration,
    string? RecordPath,
    int? DemoDisks)
{
    /// <summary>--record の秒数の上限（GIF が大きくなりすぎないように）。</summary>
    private const int MaxRecordSeconds = 60;

    public static StartupOptions Parse(IReadOnlyList<string> args)
    {
        string? language = null;
        string? screenshot = null;
        int recordSeconds = 0;
        string? record = null;
        int? demoDisks = null;
        for (int i = 0; i + 1 < args.Count; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--lang":
                    language = args[++i];
                    break;
                case "--screenshot":
                    screenshot = Path.GetFullPath(args[++i]);
                    break;
                case "--record" when i + 2 < args.Count:
                    if (int.TryParse(args[i + 1], out recordSeconds) && recordSeconds > 0)
                        record = Path.GetFullPath(args[i + 2]);
                    i += 2;
                    break;
                case "--demo-disks":
                    if (int.TryParse(args[++i], out int count))
                        demoDisks = count;
                    break;
            }
        }
        var duration = TimeSpan.FromSeconds(Math.Min(recordSeconds, MaxRecordSeconds));
        return new StartupOptions(language, screenshot, duration, record, demoDisks);
    }
}
