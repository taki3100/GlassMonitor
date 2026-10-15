using System.Text.RegularExpressions;

namespace GlassMonitor.Services;

/// <summary>ハードウェア名を表示用に短くする。</summary>
internal static partial class NameFormatter
{
    /// <summary>"Intel Core Ultra 7 265K" → "Core Ultra 7 265K"</summary>
    public static string? Cpu(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var n = Trademark().Replace(name, "");
        n = Vendor().Replace(n, "");
        n = CpuSuffix().Replace(n, "");
        return Spaces().Replace(n, " ").Trim();
    }

    /// <summary>"NVIDIA GeForce RTX 5070 Ti" → "GeForce RTX 5070 Ti"</summary>
    public static string? Gpu(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var n = Trademark().Replace(name, "");
        n = Vendor().Replace(n, "");
        return Spaces().Replace(n, " ").Trim();
    }

    /// <summary>"GeForce RTX 5070 Ti" → "RTX 5070 Ti"（ウィンドウのサブタイトル用）</summary>
    public static string? ShortGpu(string? gpu)
    {
        if (string.IsNullOrWhiteSpace(gpu))
            return null;

        var n = GpuBrand().Replace(gpu, "");
        n = GraphicsSuffix().Replace(n, "");
        return n.Trim();
    }

    /// <summary>"WD_BLACK SN8100 2000GB" → "WD_BLACK SN8100"</summary>
    public static string? Storage(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return CapacitySuffix().Replace(name.Trim(), "");
    }

    /// <summary>
    /// VRAM の規格は LibreHardwareMonitor から取れないため、主要な型番だけ名前から推定する。分からなければ null。
    /// </summary>
    public static string? GuessVramType(string? gpu)
    {
        if (string.IsNullOrWhiteSpace(gpu))
            return null;
        if (Gddr7().IsMatch(gpu))
            return "GDDR7";
        if (Gddr6X().IsMatch(gpu))
            return "GDDR6X";
        if (Gddr6().IsMatch(gpu))
            return "GDDR6";
        return null;
    }

    [GeneratedRegex(@"\((R|TM)\)", RegexOptions.IgnoreCase)]
    private static partial Regex Trademark();

    [GeneratedRegex(@"^\s*(Intel|AMD|NVIDIA)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex Vendor();

    [GeneratedRegex(@"(\s+\d+-Core)?\s+Processor\s*$|\s+CPU\s*@.*$", RegexOptions.IgnoreCase)]
    private static partial Regex CpuSuffix();

    [GeneratedRegex(@"^(GeForce|Radeon)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex GpuBrand();

    [GeneratedRegex(@"\s+Graphics$", RegexOptions.IgnoreCase)]
    private static partial Regex GraphicsSuffix();

    [GeneratedRegex(@"[\s_-]+\d+(\.\d+)?\s*(GB|TB)$", RegexOptions.IgnoreCase)]
    private static partial Regex CapacitySuffix();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"RTX\s*50[6-9]0", RegexOptions.IgnoreCase)]
    private static partial Regex Gddr7();

    [GeneratedRegex(@"RTX\s*(4090|4080|3090|3080)|RTX\s*(4070|3070)\s*Ti", RegexOptions.IgnoreCase)]
    private static partial Regex Gddr6X();

    [GeneratedRegex(@"RTX\s*(40[56]0|30[56]0)|RX\s*[6-9]\d{3}|Arc\s*[AB]\d{3}", RegexOptions.IgnoreCase)]
    private static partial Regex Gddr6();
}
