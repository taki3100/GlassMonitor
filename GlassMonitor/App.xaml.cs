using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using GlassMonitor.Capture;
using GlassMonitor.Interop;
using GlassMonitor.Localization;
using GlassMonitor.Services;
using GlassMonitor.Settings;
using GlassMonitor.ViewModels;

namespace GlassMonitor;

public partial class App : Application
{
    private HardwareMonitorService? _monitor;
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // 自動起動と手動起動が重なっても、センサーを読むのは 1 つだけにする
        _singleInstance = new Mutex(initiallyOwned: true, @"Local\GlassMonitor.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            ActivateRunningInstance();
            Shutdown();
            return;
        }

        var options = StartupOptions.Parse(e.Args);
        bool capturing = options.ScreenshotPath is not null || options.RecordPath is not null;
        var settings = capturing ? CaptureMode.CreateSettings() : SettingsStore.Load();
        // ダミーのディスクを表示している間は、ドライブの選択などを利用者の設定に保存しない
        if (options.DemoDisks is not null)
            SettingsStore.IsReadOnly = true;
        // 画面を作る前に言語を決める（--lang はこの起動だけ上書きし、設定には保存しない）
        Localizer.SetLanguage(options.Language ?? settings.Language);

        var viewModel = new MainViewModel(settings);
        var window = new MainWindow(viewModel, settings);
        MainWindow = window;
        window.Show();

        CaptureMode? capture = options.RecordPath is { } recordPath
            ? new RecordingMode(window, options.RecordDuration, recordPath)
            : options.ScreenshotPath is { } screenshotPath ? new ScreenshotMode(window, screenshotPath) : null;
        capture?.Start();

        // センサーはワーカーで読み、UI スレッドで反映する
        var monitor = new HardwareMonitorService(settings.MemoryTemperature, options.DemoDisks) { SelectedDrives = settings.SelectedDrives };
        monitor.SnapshotAvailable += snapshot =>
        {
            if (Dispatcher.HasShutdownStarted)
                return;
            Dispatcher.InvokeAsync(() =>
            {
                if (capture?.ShowsSnapshots == false)
                    return;
                viewModel.Apply(snapshot);
                capture?.OnSnapshotApplied();
            });
        };
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.MemoryTemperature))
                monitor.MemoryTemperatureEnabled = viewModel.MemoryTemperature;
            else if (args.PropertyName == nameof(MainViewModel.SelectedDrives))
                monitor.SelectedDrives = viewModel.SelectedDrives;
        };
        monitor.Start();
        _monitor = monitor;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitor?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    /// <summary>すでに起動しているウィンドウを前面に出す。</summary>
    private static void ActivateRunningInstance()
    {
        using var current = Process.GetCurrentProcess();
        foreach (var process in Process.GetProcessesByName(current.ProcessName))
        {
            using (process)
            {
                if (process.Id == current.Id || process.MainWindowHandle == IntPtr.Zero)
                    continue;

                if (NativeMethods.IsIconic(process.MainWindowHandle))
                    NativeMethods.ShowWindow(process.MainWindowHandle, NativeMethods.SW_RESTORE);
                NativeMethods.SetForegroundWindow(process.MainWindowHandle);
                return;
            }
        }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        (MainWindow as MainWindow)?.SaveSettings();
        base.OnSessionEnding(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GlassMonitor");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "error.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
    }
}
