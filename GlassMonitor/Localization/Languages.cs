using System.Globalization;

namespace GlassMonitor.Localization;

/// <summary>対応言語。Code は CultureInfo の名前（"ja"、"zh-Hans" など）、NativeName はその言語で書いた言語名。</summary>
public sealed record LanguageInfo(string Code, string NativeName);

public static class Languages
{
    /// <summary>設定の「自動」。OS の表示言語が対応言語ならその言語、そうでなければ英語にする。</summary>
    public const string Auto = "auto";

    /// <summary>Strings.resx の言語。訳の無い文字列や、対応していない言語の OS でもこれを使う。</summary>
    public const string Default = "en";

    /// <summary>
    /// 対応言語（設定画面に並べる順）。言語を追加するときは Resources/Strings.&lt;コード&gt;.resx を作り、
    /// ここに 1 行足す（手順は CONTRIBUTING.md）。
    /// </summary>
    public static IReadOnlyList<LanguageInfo> Supported { get; } =
    [
        new("ja", "日本語"),
        new("en", "English"),
    ];

    /// <summary>対応言語のコードならその表記に揃え、それ以外（"auto"、削除した言語など）は「自動」にする。</summary>
    public static string Normalize(string? language) =>
        Find(language)?.Code ?? Auto;

    /// <summary>言語の設定から表示に使う言語を決める。「自動」なら OS の表示言語から選ぶ。</summary>
    public static CultureInfo Resolve(string language, CultureInfo systemCulture)
    {
        if (Find(language) is { } chosen)
            return CultureInfo.GetCultureInfo(chosen.Code);

        // "ja-JP" → "ja" のように親をたどり、対応言語に当たればそれを使う
        for (var culture = systemCulture; culture.Name.Length > 0; culture = culture.Parent)
        {
            if (Find(culture.Name) is { } match)
                return CultureInfo.GetCultureInfo(match.Code);
        }
        return CultureInfo.GetCultureInfo(Default);
    }

    private static LanguageInfo? Find(string? code) =>
        Supported.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));
}
