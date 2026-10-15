using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using GlassMonitor.Localization;

namespace GlassMonitor.ViewModels;

/// <summary>ウィンドウ中央に重ねて表示する確認ダイアログ。</summary>
public sealed class ConfirmDialogViewModel(
    string title,
    string message,
    string confirmText,
    string? cancelText,
    Action onConfirm,
    Action onClose)
{
    public string Title { get; } = title;
    public string Message { get; } = message;
    public string ConfirmText { get; } = confirmText;

    /// <summary>null ならキャンセルボタンを出さない（お知らせ用）。</summary>
    public string? CancelText { get; } = cancelText;

    public ICommand ConfirmCommand { get; } = new RelayCommand(_ =>
    {
        onClose();
        onConfirm();
    });

    public ICommand CancelCommand { get; } = new RelayCommand(_ => onClose());
}

public enum PawnIoProblem
{
    /// <summary>PawnIO がインストールされていない。</summary>
    NotInstalled,

    /// <summary>インストールされているが使えない（アンチチートによるブロック、ドライバー未読み込みなど）。</summary>
    Unavailable,
}

/// <summary>PawnIO が無い、または使えないとき（CPU センサー制限モード）の案内。</summary>
public sealed class PawnIoGuideViewModel(
    PawnIoProblem problem,
    string? installedVersion,
    bool showOnStartup,
    Action<bool> setShowOnStartup,
    Action onClose) : ObservableObject
{
    public const string SiteUrl = "https://pawnio.eu/";
    public const string ReleasesUrl = "https://github.com/namazso/PawnIO.Setup/releases/latest";
    public const string WingetCommand = "winget install --id namazso.PawnIO --exact";

    private bool _showOnStartup = showOnStartup;

    public bool IsNotInstalled => problem == PawnIoProblem.NotInstalled;

    public string Title => Text("PawnIo_TitleNotInstalled", "PawnIo_TitleUnavailable");

    public string Description => Text("PawnIo_DescriptionNotInstalled", "PawnIo_DescriptionUnavailable");

    public string StepsTitle => Text("PawnIo_StepsNotInstalled", "PawnIo_StepsUnavailable");

    public string FirstStep => Text("PawnIo_FirstStepNotInstalled", "PawnIo_FirstStepUnavailable");

    public string LastStep => Text("PawnIo_LastStepNotInstalled", "PawnIo_LastStepUnavailable");

    public string? InstalledVersionText =>
        installedVersion is null ? null : Localizer.Format("PawnIo_InstalledVersion", installedVersion);

    /// <summary>未インストールのとき、起動時にこの案内を出すか。</summary>
    public bool ShowOnStartup
    {
        get => _showOnStartup;
        set
        {
            if (SetProperty(ref _showOnStartup, value))
                setShowOnStartup(value);
        }
    }

    public string Winget => WingetCommand;

    public ICommand OpenSiteCommand { get; } = new RelayCommand(_ => OpenUrl(SiteUrl));

    /// <summary>コマンドの下の補足。</summary>
    public string WingetHint => Text("PawnIo_WingetHintNotInstalled", "PawnIo_WingetHintUnavailable");

    public ICommand OpenReleasesCommand { get; } = new RelayCommand(_ => OpenUrl(ReleasesUrl));

    public ICommand CopyWingetCommand { get; } = new RelayCommand(_ =>
    {
        try
        {
            Clipboard.SetText(WingetCommand);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // 他のアプリがクリップボードを開いている場合は諦める
        }
    });

    public ICommand CloseCommand { get; } = new RelayCommand(_ => onClose());

    /// <summary>未インストールのときと、使えないときで文言を変える。</summary>
    private string Text(string notInstalledKey, string unavailableKey) =>
        Localizer.Get(IsNotInstalled ? notInstalledKey : unavailableKey);

    /// <summary>
    /// アプリは管理者権限で動いているため、ブラウザーを直接起動すると昇格したまま開いてしまう。
    /// explorer.exe 経由で開き、通常の権限のブラウザーに渡す。
    /// </summary>
    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{url}\"") { UseShellExecute = false });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }
}
