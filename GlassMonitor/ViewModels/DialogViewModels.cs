using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

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

    public string Title => IsNotInstalled ? "PawnIO がインストールされていません" : "PawnIO を利用できません";

    public string Description => IsNotInstalled
        ? "GlassMonitor は CPU の温度・クロック・消費電力やマザーボードのファン回転数を読み取るために、オープンソースのドライバー「PawnIO」を使います。PawnIO が無い間は CPU センサー制限モードで動作し、これらの項目は「—」と表示されます。GPU、メモリ、ストレージはそのまま表示できます。"
        : "PawnIO はインストールされていますが、CPU のセンサーを読み取れませんでした。FACEIT などのアンチチートが PawnIO をブロックしている場合や、ドライバーが読み込まれていない場合に起こります。CPU センサー制限モードで動作し、CPU の温度・クロック・消費電力とファン回転数は「—」と表示されます。GPU、メモリ、ストレージはそのまま表示できます。";

    public string StepsTitle => IsNotInstalled ? "導入方法" : "対処方法";

    public string FirstStep => IsNotInstalled
        ? "1.  公式サイトまたは GitHub のリリースから PawnIO_setup.exe をダウンロードして実行します。"
        : "1.  FACEIT などのアンチチートを使っている場合は、終了してから PC を再起動します。改善しない場合は、公式サイトまたは GitHub のリリースから PawnIO を再インストールします。";

    public string LastStep => IsNotInstalled
        ? "2.  インストールが終わったら、GlassMonitor を再起動してください。"
        : "2.  対処が終わったら、GlassMonitor を再起動してください。";

    public string? InstalledVersionText =>
        installedVersion is null ? null : $"インストール済みの PawnIO: {installedVersion}";

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
    public string WingetHint => IsNotInstalled
        ? "winget を使う場合は、ターミナルで次のコマンドを実行します。"
        : "winget を使う場合は、次のコマンドで最新版に更新できます。";

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
