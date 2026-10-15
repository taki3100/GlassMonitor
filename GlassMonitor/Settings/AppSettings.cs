using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GlassMonitor.Settings;

/// <summary>ウィンドウの通常時の矩形（物理ピクセル、ワークスペース座標）と最大化状態。</summary>
public sealed record WindowBounds(int Left, int Top, int Right, int Bottom, bool Maximized);

/// <summary>この温度（℃）以上で「高め」/「高温」。</summary>
public sealed record TemperatureThresholds(int Warm, int Hot)
{
    public static TemperatureThresholds ChipDefault { get; } = new(70, 85);
    public static TemperatureThresholds StorageDefault { get; } = new(60, 75);

    [JsonIgnore]
    public bool IsValid => Warm is >= 1 and <= 150 && Hot is >= 1 and <= 150 && Warm < Hot;
}

public sealed class AppSettings
{
    public const string DefaultAccent = "#9BE7FF";

    public WindowBounds? Window { get; set; }
    public bool Topmost { get; set; }
    public string Accent { get; set; } = DefaultAccent;
    public bool AmbientGlow { get; set; } = true;

    /// <summary>
    /// SMBus 経由で DIMM の SPD から温度を読むか。iCUE がメモリのライティングを SMBus で制御するため既定は OFF。
    /// </summary>
    public bool MemoryTemperature { get; set; }

    /// <summary>CPU・GPU・メモリの温度しきい値。</summary>
    public TemperatureThresholds ChipThresholds { get; set; } = TemperatureThresholds.ChipDefault;

    /// <summary>SSD（ストレージ）の温度しきい値。</summary>
    public TemperatureThresholds StorageThresholds { get; set; } = TemperatureThresholds.StorageDefault;

    /// <summary>ディスクが 5 台以上あるときに表示するディスクのキー（最大 4 台）。空なら先頭の 4 台。</summary>
    public List<string> SelectedDrives { get; set; } = [];

    /// <summary>PawnIO が未インストールのとき、起動時に導入方法を案内するか。</summary>
    public bool ShowPawnIoGuideOnStartup { get; set; } = true;
}

/// <summary>%APPDATA%\GlassMonitor\settings.json に設定を保存する。</summary>
public static class SettingsStore
{
    private static readonly string SettingsDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlassMonitor");

    private static readonly string FilePath = Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        var settings = new AppSettings();
        try
        {
            if (File.Exists(FilePath))
                settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // 壊れた設定ファイルは無視して既定値で起動する
        }

        if (settings.ChipThresholds is not { IsValid: true })
            settings.ChipThresholds = TemperatureThresholds.ChipDefault;
        if (settings.StorageThresholds is not { IsValid: true })
            settings.StorageThresholds = TemperatureThresholds.StorageDefault;
        return settings;
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }
}
