using System.IO;

namespace GlassMonitor;

/// <summary>
/// 起動オプション。
/// <list type="bullet">
/// <item><c>--lang &lt;auto|ja|en …&gt;</c>: この起動だけ表示言語を切り替える（設定は変えない）。</item>
/// <item><c>--screenshot &lt;file.png&gt;</c>: README 用のスクリーンショットを撮って終了する（ScreenshotMode）。</item>
/// </list>
/// </summary>
internal sealed record StartupOptions(string? Language, string? ScreenshotPath)
{
    public static StartupOptions Parse(IReadOnlyList<string> args)
    {
        string? language = null;
        string? screenshot = null;
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
            }
        }
        return new StartupOptions(language, screenshot);
    }
}
