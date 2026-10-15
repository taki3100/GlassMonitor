using System.ComponentModel;
using System.Globalization;
using System.Resources;
using System.Windows.Data;

namespace GlassMonitor.Localization;

/// <summary>
/// 画面の文字列（Resources/Strings.resx と、言語ごとの Strings.&lt;コード&gt;.resx）を返す。
/// XAML からは {l:Loc キー}、コードからは Get / Format で使う。訳の無いキーは英語（Strings.resx）になる。
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    private static readonly ResourceManager Strings =
        new("GlassMonitor.Resources.Strings", typeof(Localizer).Assembly);

    // 「自動」で使う OS の表示言語。切り替えると CurrentUICulture が変わるため、最初に切り替える前の値を覚えておく
    private static CultureInfo? _systemCulture;

    private Localizer()
    {
    }

    /// <summary>XAML のバインド元。言語を切り替えるとインデクサーの変更を通知する。</summary>
    public static Localizer Instance { get; } = new();

    /// <summary>言語の設定（"auto" または対応言語のコード）。</summary>
    public static string Language { get; private set; } = Languages.Auto;

    /// <summary>表示中の言語。</summary>
    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo(Languages.Default);

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => Get(key);

    /// <summary>リソースに無いキーはキーをそのまま返す（訳し漏れではなく、キーの書き間違いに気づけるように）。</summary>
    public static string Get(string key) => Strings.GetString(key, Culture) ?? key;

    /// <summary>"{0} drives" のような書式の文字列に値を入れる。</summary>
    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, Get(key), args);

    /// <summary>言語の設定（"auto" / "ja" / "en" など）を反映する。</summary>
    public static void SetLanguage(string? language)
    {
        _systemCulture ??= CultureInfo.CurrentUICulture;
        Language = Languages.Normalize(language);
        Culture = Languages.Resolve(Language, _systemCulture);

        // WPF 自身の文字列（テキストボックスの右クリックメニューなど）も同じ言語にする
        CultureInfo.CurrentUICulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;

        Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(Binding.IndexerName));
    }
}
