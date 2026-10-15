using System.Globalization;

namespace GlassMonitor.ViewModels;

/// <summary>表示用の数値フォーマット。取得できない値は「—」。</summary>
internal static class Format
{
    public const string Missing = "—";

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Number(double? value, string format) =>
        value is double v ? v.ToString(format, Culture) : Missing;

    public static string WithUnit(double? value, string format, string unit) =>
        value is double v ? $"{v.ToString(format, Culture)} {unit}" : Missing;

    public static string Percent(double? value) =>
        value is double v ? $"{v.ToString("0", Culture)}%" : Missing;

    public static string Gigahertz(double? mhz) => WithUnit(mhz / 1000, "0.0", "GHz");

    /// <summary>10 進の GB を受け取り、TB か GB で表示する（同じドライブの値は単位を揃える）。</summary>
    public static string Capacity(double? gb, bool terabytes) =>
        terabytes ? WithUnit(gb / 1000, "0.00", "TB") : WithUnit(gb, "0", "GB");

    public static string MegabytesPerSecond(double? mbps) =>
        WithUnit(mbps, mbps < 10 ? "0.0" : "N0", "MB/s");
}
