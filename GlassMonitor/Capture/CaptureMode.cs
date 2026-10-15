using System.Windows;
using GlassMonitor.Settings;
using GlassMonitor.ViewModels;

namespace GlassMonitor.Capture;

/// <summary>
/// --screenshot / --record の共通部分。既定の設定・既定のサイズで表示し、グラフが 60 秒分たまったら撮影を始める。
/// 利用者の設定は読み込まず、保存もしない（ウィンドウ位置やアクセント色を書き換えないため）。
/// </summary>
internal abstract class CaptureMode
{
    private int _snapshots;

    /// <summary>センサーの値を表示に反映するか。静止画は、撮影の準備に入ったら反映を止める。</summary>
    public bool ShowsSnapshots { get; protected set; } = true;

    /// <summary>撮影用の設定。起動時の PawnIO の案内も出さない。</summary>
    public static AppSettings CreateSettings()
    {
        SettingsStore.IsReadOnly = true;
        return new AppSettings { ShowPawnIoGuideOnStartup = false };
    }

    /// <summary>ウィンドウを表示したあとに呼ぶ。</summary>
    public virtual void Start()
    {
    }

    public void OnSnapshotApplied()
    {
        if (++_snapshots == TileViewModel.HistoryLength)
            Begin();
    }

    /// <summary>グラフが 60 秒分たまったところで呼ばれる。</summary>
    protected abstract void Begin();

    /// <summary>撮影を終えてアプリを終了する。失敗したときは理由を表示する。</summary>
    protected static void Exit(Exception? error)
    {
        if (error is not null)
            MessageBox.Show(error.Message, "GlassMonitor", MessageBoxButton.OK, MessageBoxImage.Error);
        Application.Current.Shutdown(error is null ? 0 : 1);
    }
}
