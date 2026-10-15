using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Media;
using GlassMonitor.Localization;
using GlassMonitor.Services;
using GlassMonitor.Settings;

namespace GlassMonitor.ViewModels;

public sealed class AccentOption(string hex) : ObservableObject
{
    private bool _isSelected;

    public string Hex { get; } = hex;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>設定画面の言語の選択肢。Code は "auto" または対応言語のコード。</summary>
public sealed class LanguageOption(string code, string? nativeName) : ObservableObject
{
    private bool _isSelected;

    public string Code { get; } = code;

    /// <summary>言語名はその言語で書く（English / 日本語）。「自動」だけは表示中の言語にする。</summary>
    public string Name => nativeName ?? Localizer.Get("Settings_LanguageAuto");

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class MainViewModel : ObservableObject
{
    private static readonly string[] AccentPresets =
        [AppSettings.DefaultAccent, "#8CF5C8", "#C9B6FF", "#FFB3D6", "#FFD07A", "#FFFFFF"];

    private readonly AppSettings _settings;

    private readonly string _appVersion;
    private string? _systemSummary;
    private string _lastUpdated = Format.Missing;
    private string _accentHex = AppSettings.DefaultAccent;
    private MemoryReading? _lastMemory;
    private object? _activeDialog;
    private bool _autoStart;
    private readonly Version? _pawnIoVersion;
    private bool _isCpuSensorLimited;
    private bool _isStorageMulti;

    public MainViewModel(AppSettings settings)
    {
        _settings = settings;

        Cpu = new TileViewModel("Tile_Cpu", "Tile_Temperature", "Tile_Clock", "Tile_Power", "Tile_Fan");
        Gpu = new TileViewModel("Tile_Gpu", "Tile_Temperature", "Tile_Clock", "Tile_Power", "Tile_Fan");
        Memory = new TileViewModel("Tile_Memory", "Tile_Temperature", "Tile_InUse", "Tile_Available", "Tile_Speed");
        Vram = new TileViewModel("Tile_Vram", "Tile_Used", "Tile_Free", "Tile_Clock", "Tile_MemoryController");
        Storage = new StorageTileViewModel();
        ApplyThresholds();
        ShowMemory();

        ThresholdEditors =
        [
            new ThresholdEditor("Settings_ThresholdChips", () => _settings.ChipThresholds, t =>
            {
                _settings.ChipThresholds = t;
                ApplyThresholds();
                SettingsStore.Save(_settings);
            }),
            new ThresholdEditor("Settings_ThresholdStorage", () => _settings.StorageThresholds, t =>
            {
                _settings.StorageThresholds = t;
                ApplyThresholds();
                SettingsStore.Save(_settings);
            }),
        ];

        AccentOptions = AccentPresets.Select(h => new AccentOption(h)).ToArray();
        SelectAccentCommand = new RelayCommand(p =>
        {
            if (p is string hex)
                AccentHex = hex;
        });

        LanguageOptions =
        [
            new LanguageOption(Languages.Auto, null),
            .. Languages.Supported.Select(l => new LanguageOption(l.Code, l.NativeName)),
        ];
        SelectLanguageCommand = new RelayCommand(p =>
        {
            if (p is string code)
                SelectLanguage(code);
        });
        UpdateLanguageSelection();

        IsAdministrator = SystemStatus.IsAdministrator();
        _pawnIoVersion = SystemStatus.PawnIoVersion;
        _autoStart = AutoStartTask.IsRegistered();
        ShowPawnIoGuideCommand = new RelayCommand(_ => ShowPawnIoGuide());

        _appVersion = typeof(MainViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";

        if (!TryApplyAccent(settings.Accent))
            TryApplyAccent(AppSettings.DefaultAccent);

        // PawnIO が無いと CPU 温度などが取れないため、起動時に導入方法を案内する（案内の中で次回から出さないこともできる）
        if (_pawnIoVersion is null)
        {
            _isCpuSensorLimited = true;
            if (settings.ShowPawnIoGuideOnStartup)
                ShowPawnIoGuide();
        }
    }

    /// <summary>
    /// CPU センサー制限モード。PawnIO が無い、またはブロックされていて CPU の温度・消費電力が読めない状態。
    /// この間も PawnIO の要らない項目は表示を続け、ステータスバーから導入方法を案内する。
    /// </summary>
    public bool IsCpuSensorLimited
    {
        get => _isCpuSensorLimited;
        private set => SetProperty(ref _isCpuSensorLimited, value);
    }

    /// <summary>"GlassMonitor 0.2.0 · PawnIO 2.2.0"（不具合や互換性の報告用）</summary>
    public string VersionText =>
        $"GlassMonitor {_appVersion} · PawnIO {FormatVersion(_pawnIoVersion) ?? Localizer.Get("PawnIo_NotInstalled")}";

    /// <summary>ディスクが 2 台以上ならカード表示、1 台なら詳細表示。</summary>
    public bool IsStorageMulti
    {
        get => _isStorageMulti;
        private set => SetProperty(ref _isStorageMulti, value);
    }

    public StorageDrivesViewModel StorageDrives { get; } = new();

    /// <summary>設定画面のディスク選択（5 台以上あるときだけ表示）。</summary>
    public ObservableCollection<DriveChoiceViewModel> DriveChoices { get; } = [];

    public bool HasDriveChoices => DriveChoices.Count > DriveReader.MaxDisplayed;

    /// <summary>表示するディスクのキー（設定で選んだもの）。</summary>
    public IReadOnlyList<string> SelectedDrives => _settings.SelectedDrives;

    /// <summary>ウィンドウ中央に重ねて表示するダイアログ（null なら非表示）。</summary>
    public object? ActiveDialog
    {
        get => _activeDialog;
        private set => SetProperty(ref _activeDialog, value);
    }

    public ICommand ShowPawnIoGuideCommand { get; }

    /// <summary>
    /// サインイン時の自動起動。ON にするときは確認ダイアログを出し、承認されてからタスクを登録する。
    /// OFF にするとすぐに解除する。
    /// </summary>
    public bool AutoStart
    {
        get => _autoStart;
        set
        {
            if (value != _autoStart)
            {
                if (value)
                    ConfirmAutoStart();
                else
                    UpdateAutoStart(AutoStartTask.Unregister, "AutoStart_UnregisterFailed");
            }
            // 確認が済むまではトグルを元の状態に戻しておくため常に通知する
            OnPropertyChanged();
        }
    }

    public TileViewModel Cpu { get; }
    public TileViewModel Gpu { get; }
    public TileViewModel Memory { get; }
    public TileViewModel Vram { get; }
    public StorageTileViewModel Storage { get; }

    public IReadOnlyList<ThresholdEditor> ThresholdEditors { get; }

    public IReadOnlyList<AccentOption> AccentOptions { get; }

    public ICommand SelectAccentCommand { get; }

    /// <summary>言語の選択肢（自動 / 日本語 / English …）。</summary>
    public IReadOnlyList<LanguageOption> LanguageOptions { get; }

    public ICommand SelectLanguageCommand { get; }

    public bool IsAdministrator { get; }

    public string AdminText => Localizer.Get(IsAdministrator ? "Status_Administrator" : "Status_NotAdministrator");

    /// <summary>"Core Ultra 7 265K · RTX 5070 Ti · 64 GB"</summary>
    public string SystemSummary
    {
        get => _systemSummary ?? Localizer.Get("Status_Initializing");
        private set => SetProperty(ref _systemSummary, value);
    }

    public string LastUpdated
    {
        get => _lastUpdated;
        private set => SetProperty(ref _lastUpdated, value);
    }

    public bool Topmost
    {
        get => _settings.Topmost;
        set
        {
            if (_settings.Topmost == value)
                return;
            _settings.Topmost = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
            OnPropertyChanged(nameof(TopmostText));
        }
    }

    public string TopmostText => Localizer.Get(Topmost ? "Status_TopmostOn" : "Status_TopmostOff");

    public bool AmbientGlow
    {
        get => _settings.AmbientGlow;
        set
        {
            if (_settings.AmbientGlow == value)
                return;
            _settings.AmbientGlow = value;
            SettingsStore.Save(_settings);
            OnPropertyChanged();
        }
    }

    /// <summary>メモリ温度（SMBus 経由の SPD 読み取り）の ON/OFF。</summary>
    public bool MemoryTemperature
    {
        get => _settings.MemoryTemperature;
        set
        {
            if (_settings.MemoryTemperature == value)
                return;
            _settings.MemoryTemperature = value;
            SettingsStore.Save(_settings);
            ShowMemory();
            OnPropertyChanged();
        }
    }

    /// <summary>アクセント色（#RRGGBB）。不正な値は無視して元に戻す。</summary>
    public string AccentHex
    {
        get => _accentHex;
        set
        {
            if (TryApplyAccent(value))
                SettingsStore.Save(_settings);
            // 不正な入力でもテキストボックスを現在値に戻すため常に通知する
            OnPropertyChanged();
        }
    }

    public void Apply(HardwareSnapshot snapshot)
    {
        var cpu = snapshot.Cpu;
        Cpu.Subtitle = cpu.Name ?? "";
        Cpu.PushUsage(cpu.Load);
        Cpu.SetTemperature(cpu.Temperature);
        Cpu.SetRows(
            Format.Gigahertz(cpu.ClockMhz),
            Format.WithUnit(cpu.PowerWatts, "0", "W"),
            Format.WithUnit(cpu.FanRpm, "N0", "rpm"));

        var gpu = snapshot.Gpu;
        Gpu.Subtitle = gpu.Name ?? "";
        Gpu.PushUsage(gpu.Load);
        Gpu.SetTemperature(gpu.Temperature);
        Gpu.SetRows(
            Format.WithUnit(gpu.ClockMhz, "N0", "MHz"),
            Format.WithUnit(gpu.PowerWatts, "0", "W"),
            Format.WithUnit(gpu.FanRpm, "N0", "rpm"));

        var mem = snapshot.Memory;
        string? installed = mem.InstalledGb is double g ? $"{Format.Number(g, "0")} GB" : null;
        string? spec = mem.Type is null ? null : mem.SpeedMts is int s ? $"{mem.Type}-{s}" : mem.Type;
        Memory.Subtitle = Join(installed, spec);
        Memory.PushUsage(mem.Load);
        _lastMemory = mem;
        ShowMemory();

        double? totalGb = gpu.MemoryTotalMb / 1024;
        double? usedGb = gpu.MemoryUsedMb / 1024;
        double? freeGb = gpu.MemoryFreeMb / 1024;
        Vram.Subtitle = Join(
            totalGb is double t ? $"{Format.Number(Math.Round(t), "0")} GB" : null,
            NameFormatter.GuessVramType(gpu.Name));
        Vram.PushUsage(totalGb > 0 ? usedGb / totalGb * 100 : null);
        Vram.SetPrimary(usedGb, "0.0", "GB", totalGb is double total ? $"/ {Format.Number(total, "0.0")} GB" : null);
        Vram.SetRows(
            Format.WithUnit(freeGb, "0.0", "GB"),
            Format.WithUnit(gpu.MemoryClockMhz, "N0", "MHz"),
            Format.Percent(gpu.MemoryControllerLoad));

        ShowDrives(snapshot.Drives);
        UpdateDriveChoices(snapshot.AvailableDrives);

        // PawnIO が無い・ブロックされている場合は、CPU の温度と消費電力がどちらも取れない
        IsCpuSensorLimited = cpu.Temperature is null && cpu.PowerWatts is null;

        string summary = Join(cpu.Name, NameFormatter.ShortGpu(gpu.Name), installed);
        SystemSummary = summary.Length > 0 ? summary : Localizer.Get("Status_NoSensors");
        LastUpdated = snapshot.Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }

    /// <summary>1 台なら詳細表示（使用容量リング＋読込/書込グラフ）、2〜4 台ならカードを並べる。</summary>
    private void ShowDrives(IReadOnlyList<DriveReading> drives)
    {
        IsStorageMulti = drives.Count > 1;
        if (IsStorageMulti)
        {
            StorageDrives.Update(drives, _settings.StorageThresholds);
            return;
        }

        var disk = drives.Count == 1 ? drives[0] : null;
        Storage.Subtitle = disk is null ? "" : Join(disk.Model, disk.Kind, disk.Letters);
        Storage.PushUsage(disk?.UsedPercent);
        Storage.SetTemperature(disk?.Temperature);
        bool terabytes = disk?.TotalGb >= 1000;
        Storage.SetRows(
            Format.Capacity(disk?.UsedGb, terabytes),
            Format.Capacity(disk?.FreeGb, terabytes),
            disk is null ? Format.Missing : DriveCardViewModel.HealthTextOf(disk));
        Storage.PushThroughput(disk?.ReadBytesPerSecond, disk?.WriteBytesPerSecond, disk?.ActivityPercent);
    }

    /// <summary>設定画面のディスク一覧を、検出したディスクに合わせる。</summary>
    private void UpdateDriveChoices(IReadOnlyList<DriveChoice> available)
    {
        if (!DriveChoices.Select(c => c.Key).SequenceEqual(available.Select(a => a.Key)))
        {
            DriveChoices.Clear();
            foreach (var drive in available)
                DriveChoices.Add(new DriveChoiceViewModel(drive, ToggleDrive));
            OnPropertyChanged(nameof(HasDriveChoices));
        }

        // 表示の選び方は DriveReader と同じ（保存した選択が無ければ先頭の 4 台）
        var selected = DriveChoices.Where(c => _settings.SelectedDrives.Contains(c.Key)).Take(DriveReader.MaxDisplayed).ToList();
        if (selected.Count == 0)
            selected = DriveChoices.Take(DriveReader.MaxDisplayed).ToList();
        foreach (var choice in DriveChoices)
            choice.SetSelected(selected.Contains(choice));
        RefreshDriveChoiceAvailability();
    }

    private void ToggleDrive(DriveChoiceViewModel choice, bool select)
    {
        var keys = DriveChoices.Where(c => c.IsSelected).Select(c => c.Key).ToList();
        if (select)
        {
            if (keys.Count >= DriveReader.MaxDisplayed)
                return;
            keys.Add(choice.Key);
        }
        else
        {
            // 1 台は必ず表示する
            if (keys.Count <= 1)
                return;
            keys.Remove(choice.Key);
        }

        choice.SetSelected(select);
        _settings.SelectedDrives = keys;
        SettingsStore.Save(_settings);
        OnPropertyChanged(nameof(SelectedDrives));
        RefreshDriveChoiceAvailability();
    }

    private void RefreshDriveChoiceAvailability()
    {
        bool full = DriveChoices.Count(c => c.IsSelected) >= DriveReader.MaxDisplayed;
        foreach (var choice in DriveChoices)
            choice.IsEnabled = choice.IsSelected || !full;
    }

    /// <summary>"2.2.0.0" → "2.2.0"</summary>
    private static string? FormatVersion(Version? version) =>
        version is null ? null : version.Revision > 0 ? version.ToString() : version.ToString(3);

    private void ShowPawnIoGuide() => ActiveDialog = new PawnIoGuideViewModel(
        _pawnIoVersion is null ? PawnIoProblem.NotInstalled : PawnIoProblem.Unavailable,
        FormatVersion(_pawnIoVersion),
        _settings.ShowPawnIoGuideOnStartup,
        show =>
        {
            _settings.ShowPawnIoGuideOnStartup = show;
            SettingsStore.Save(_settings);
        },
        CloseDialog);

    /// <summary>
    /// メモリタイルの大きな数字とサブ情報。温度を読む設定なら温度を、読まないなら VRAM と同じく使用量を大きく表示する。
    /// 設定を切り替えたときは最後の値で描き直し、次の更新を待たずに表示を切り替える。
    /// </summary>
    private void ShowMemory()
    {
        var mem = _lastMemory;
        string used = Format.WithUnit(mem?.UsedGb, "0.0", "GB");
        string free = Format.WithUnit(mem?.AvailableGb, "0.0", "GB");
        string speed = mem?.SpeedMts is int mts ? $"{mts.ToString(CultureInfo.InvariantCulture)} MT/s" : Format.Missing;

        if (MemoryTemperature)
        {
            Memory.SetLayout("Tile_Temperature", "Tile_InUse", "Tile_Available", "Tile_Speed");
            Memory.SetTemperature(mem?.Temperature);
            Memory.SetRows(used, free, speed);
        }
        else
        {
            Memory.SetLayout("Tile_Used", "Tile_Available", "Tile_Speed", "Tile_Temperature");
            Memory.SetPrimary(mem?.UsedGb, "0.0", "GB",
                mem?.TotalGb is double total ? $"/ {Format.Number(total, "0.0")} GB" : null);
            Memory.SetRows(free, speed, Format.Missing);
        }
    }

    private void ConfirmAutoStart()
    {
        ActiveDialog = new ConfirmDialogViewModel(
            Localizer.Get("AutoStart_ConfirmTitle"),
            Localizer.Format("AutoStart_ConfirmMessage", AutoStartTask.TaskName),
            Localizer.Get("AutoStart_Register"),
            Localizer.Get("Common_Cancel"),
            onConfirm: () => UpdateAutoStart(AutoStartTask.Register, "AutoStart_RegisterFailed"),
            onClose: CloseDialog);
    }

    private void UpdateAutoStart(Action change, string failureTitleKey)
    {
        try
        {
            change();
        }
        catch (AutoStartException ex)
        {
            ActiveDialog = new ConfirmDialogViewModel(
                Localizer.Get(failureTitleKey), ex.Message, Localizer.Get("Common_Close"), null, () => { }, CloseDialog);
        }

        _autoStart = AutoStartTask.IsRegistered();
        OnPropertyChanged(nameof(AutoStart));
    }

    private void CloseDialog() => ActiveDialog = null;

    private void SelectLanguage(string code)
    {
        if (code == Localizer.Language)
            return;

        _settings.Language = code;
        SettingsStore.Save(_settings);
        Localizer.SetLanguage(code);
        UpdateLanguageSelection();
        RefreshText();
    }

    private void UpdateLanguageSelection()
    {
        foreach (var option in LanguageOptions)
            option.IsSelected = option.Code == Localizer.Language;
    }

    /// <summary>
    /// 言語を切り替えたあと、表示中の文字列を読み直させる（XAML の {l:Loc} は Localizer が通知する）。
    /// センサーの値に付く文字（「良好」「空き少」など）は、次の更新（1 秒以内）で切り替わる。
    /// </summary>
    private void RefreshText()
    {
        TileViewModel[] tiles = [Cpu, Gpu, Memory, Vram, Storage];
        ObservableObject[] items =
        [
            this,
            StorageDrives,
            .. tiles,
            .. tiles.SelectMany(t => t.Rows),
            .. ThresholdEditors,
            .. DriveChoices,
            .. LanguageOptions,
        ];
        foreach (var item in items)
            item.RefreshAll();
        (ActiveDialog as ObservableObject)?.RefreshAll();
    }

    private void ApplyThresholds()
    {
        Cpu.Thresholds = _settings.ChipThresholds;
        Gpu.Thresholds = _settings.ChipThresholds;
        Memory.Thresholds = _settings.ChipThresholds;
        Storage.Thresholds = _settings.StorageThresholds;
    }

    private bool TryApplyAccent(string? text)
    {
        if (!TryParseColor(text, out var color))
            return false;

        _accentHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        _settings.Accent = _accentHex;
        Theme.ApplyAccent(color);
        foreach (var option in AccentOptions)
            option.IsSelected = string.Equals(option.Hex, _accentHex, StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static bool TryParseColor(string? text, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var hex = text.Trim();
        if (!hex.StartsWith('#'))
            hex = "#" + hex;
        if (hex.Length is not (4 or 7 or 9))
            return false;

        try
        {
            if (ColorConverter.ConvertFromString(hex) is not Color parsed)
                return false;
            color = Color.FromRgb(parsed.R, parsed.G, parsed.B); // アクセントは不透明に固定
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string Join(params string?[] parts) =>
        string.Join(" · ", parts.Where(p => !string.IsNullOrEmpty(p)));
}
