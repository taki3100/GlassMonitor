using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using GlassMonitor.Interop;
using GlassMonitor.Settings;
using GlassMonitor.ViewModels;

namespace GlassMonitor;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;

    public MainWindow(MainViewModel viewModel, AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        DataContext = viewModel;

        if (settings.Window is null)
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

        // ダイアログを出すときは設定のポップアップを閉じる（ポップアップは常に前面に出るため）
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.ActiveDialog) && viewModel.ActiveDialog is not null)
                SettingsButton.IsChecked = false;
        };

        StateChanged += (_, _) => UpdateMaximizedLayout();
        DpiChanged += (_, _) => UpdateMaximizedLayout();
        // 最小サイズの適用後に判定したいので、初回描画のあとで行う
        ContentRendered += (_, _) => WindowPlacement.EnsureWithinWorkArea(this);
    }

    /// <summary>ウィンドウ位置・サイズを含めて設定を保存する。</summary>
    public void SaveSettings()
    {
        _settings.Window = WindowPlacement.Capture(this) ?? _settings.Window;
        SettingsStore.Save(_settings);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (!WindowBackdrop.TryApplyAcrylic(this))
            Background = (Brush)FindResource("FallbackWindowBrush");

        WindowPlacement.Restore(this, _settings.Window);
        UpdateMaximizedLayout();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SaveSettings();
        base.OnClosing(e);
    }

    /// <summary>
    /// WindowChrome で最大化するとリサイズ枠の分だけ画面外にはみ出すため、その分だけ内側に寄せる。
    /// </summary>
    private void UpdateMaximizedLayout()
    {
        bool maximized = WindowState == WindowState.Maximized;
        MaximizeIcon.Data = (Geometry)FindResource(maximized ? "IconRestore" : "IconMaximize");

        if (!maximized)
        {
            RootGrid.Margin = new Thickness(0);
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        uint dpiValue = (uint)Math.Round(dpi.PixelsPerInchX);
        double frameX = NativeMethods.GetSystemMetricsForDpi(NativeMethods.SM_CXSIZEFRAME, dpiValue)
            + NativeMethods.GetSystemMetricsForDpi(NativeMethods.SM_CXPADDEDBORDER, dpiValue);
        double frameY = NativeMethods.GetSystemMetricsForDpi(NativeMethods.SM_CYSIZEFRAME, dpiValue)
            + NativeMethods.GetSystemMetricsForDpi(NativeMethods.SM_CXPADDEDBORDER, dpiValue);
        RootGrid.Margin = new Thickness(
            frameX / dpi.DpiScaleX, frameY / dpi.DpiScaleY, frameX / dpi.DpiScaleX, frameY / dpi.DpiScaleY);
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            SystemCommands.RestoreWindow(this);
        else
            SystemCommands.MaximizeWindow(this);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    /// <summary>設定画面の入力欄は Enter で確定する（フォーカスを外したときも確定する）。</summary>
    private void TextBox_CommitOnEnter(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox box)
            return;
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        box.SelectAll();
        e.Handled = true;
    }
}
