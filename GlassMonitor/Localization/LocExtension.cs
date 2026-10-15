using System.Windows.Data;
using System.Windows.Markup;

namespace GlassMonitor.Localization;

/// <summary>
/// XAML で画面の文字列を表示する（例: Text="{l:Loc Settings_Title}"）。
/// Localizer へのバインドになるため、言語を切り替えるとすぐに表示が変わる。
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class LocExtension(string key) : MarkupExtension
{
    public LocExtension()
        : this("")
    {
    }

    /// <summary>Resources/Strings.resx のキー。</summary>
    [ConstructorArgument("key")]
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Localizer.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
