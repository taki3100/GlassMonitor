using LibreHardwareMonitor.Hardware;

namespace GlassMonitor.Services;

/// <summary>
/// LibreHardwareMonitorLib からセンサー値を読み取り <see cref="HardwareSnapshot"/> にまとめる。
/// スレッドセーフではないため、単一のワーカースレッドからのみ使うこと。
/// </summary>
/// <remarks>
/// ファンとライティングは iCUE が管理しているため、Corsair 製デバイスに触れうる処理は行わない。
/// <list type="bullet">
/// <item>USB HID を使うグループ（Controller: Commander / AIO など、PSU: Corsair 電源）は無効。</item>
/// <item>
/// LibreHardwareMonitor 0.9.6 は IsMemoryEnabled = true だと Open 時に必ず SMBus 上の DIMM（SPD）を走査する。
/// そのため IsMemoryEnabled はメモリ温度の設定が ON のときだけ有効にし、OFF の間は SMBus に一切アクセスしない。
/// メモリ使用量はこの設定に関係なく GlobalMemoryStatusEx から読む。
/// </item>
/// <item>ストレージは表示中のディスク（最大 4 台）だけを更新し、Corsair 製のディスクは扱わない。</item>
/// </list>
/// </remarks>
internal sealed class SensorReader : IDisposable
{
    private readonly Computer _computer;
    private readonly DriveReader _drives = new();
    private readonly DemoDrives? _demoDrives;

    private MemoryModules _modules;
    private IHardware? _cpu;
    private IHardware? _gpu;
    private IHardware? _board;

    /// <param name="demoDisks">--demo-disks の台数。指定したときは実際のディスクを読まず、ダミーのディスクを表示する。</param>
    public SensorReader(bool memoryTemperature, int? demoDisks)
    {
        _demoDrives = demoDisks is int count ? new DemoDrives(count) : null;
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true,   // SuperIO（LPC）の CPU ファン
            IsStorageEnabled = _demoDrives is null,
            IsMemoryEnabled = memoryTemperature,
            IsControllerEnabled = false,
            IsPsuEnabled = false,
            IsNetworkEnabled = false,
            IsBatteryEnabled = false,
            IsPowerMonitorEnabled = false,
        };
    }

    public void Open()
    {
        _computer.Open();
        _modules = MemoryModules.Query();

        var all = _computer.Hardware.ToList();
        _cpu = all.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        _board = all.FirstOrDefault(h => h.HardwareType == HardwareType.Motherboard);

        // VRAM 容量で選ぶため、GPU だけは一度すべて更新してから選ぶ
        var gpus = all.Where(IsGpu).ToList();
        foreach (var gpu in gpus)
            gpu.Update();
        _gpu = SelectGpu(gpus);
    }

    /// <summary>メモリ温度（SPD 読み取り）の ON/OFF。LibreHardwareMonitor は開いたままでも切り替えられる。</summary>
    public void SetMemoryTemperatureEnabled(bool enabled)
    {
        if (_computer.IsMemoryEnabled != enabled)
            _computer.IsMemoryEnabled = enabled;
    }

    /// <param name="selectedDrives">5 台以上あるときに表示するディスクのキー（設定で選んだもの）。</param>
    public HardwareSnapshot Read(IReadOnlyCollection<string> selectedDrives)
    {
        // 表示に使うハードウェアだけを更新する（ディスクは DriveReader が表示する分だけ更新する）
        UpdateRecursive(_cpu);
        UpdateRecursive(_gpu);
        UpdateRecursive(_board);

        var hardware = _computer.Hardware.ToList();
        List<IHardware> dimms = _computer.IsMemoryEnabled ? hardware.Where(IsDimm).ToList() : [];
        foreach (var dimm in dimms)
            dimm.Update();

        var (drives, availableDrives) = _demoDrives?.Read(selectedDrives) ?? _drives.Read(hardware, selectedDrives);

        return new HardwareSnapshot(
            ReadCpu(_cpu, _board),
            ReadGpu(_gpu),
            ReadMemory(dimms),
            drives,
            availableDrives,
            DateTime.Now);
    }

    public void Dispose() => _computer.Close();

    private static CpuReading ReadCpu(IHardware? cpu, IHardware? board)
    {
        double? fan = ReadCpuFan(board);
        if (cpu is null)
            return new CpuReading(null, null, null, null, null, fan);

        // コアごとのクロックの最大値（"Bus Speed" は除外）
        double? clock = Max(Sensors(cpu, SensorType.Clock)
            .Where(s => s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
            .Select(Value));

        return new CpuReading(
            NameFormatter.Cpu(cpu.Name),
            Value(Find(cpu, SensorType.Load, "CPU Total")),
            Value(Find(cpu, SensorType.Temperature,
                "CPU Package", "Core (Tctl/Tdie)", "Tctl/Tdie", "Core (Tctl)", "Core Max", "Core Average")),
            clock,
            Value(Find(cpu, SensorType.Power, "CPU Package", "Package")),
            fan);
    }

    private static double? ReadCpuFan(IHardware? board)
    {
        if (board is null)
            return null;

        var fans = Sensors(board, SensorType.Fan).ToList();
        var fan = fans.FirstOrDefault(s => s.Name.Equals("CPU Fan", StringComparison.OrdinalIgnoreCase))
            ?? fans.FirstOrDefault(s => s.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase));
        return Value(fan);
    }

    private static GpuReading ReadGpu(IHardware? gpu)
    {
        if (gpu is null)
            return GpuReading.Empty;

        double? total = Value(Find(gpu, SensorType.SmallData, "GPU Memory Total", "D3D Dedicated Memory Total"));
        double? used = Value(Find(gpu, SensorType.SmallData, "GPU Memory Used", "D3D Dedicated Memory Used"));
        double? free = Value(Find(gpu, SensorType.SmallData, "GPU Memory Free", "D3D Dedicated Memory Free"))
            ?? total - used;

        return new GpuReading(
            NameFormatter.Gpu(gpu.Name),
            Value(Find(gpu, SensorType.Load, "GPU Core", "D3D 3D")),
            Value(Find(gpu, SensorType.Temperature, "GPU Core", "GPU Hot Spot")
                ?? Sensors(gpu, SensorType.Temperature).FirstOrDefault()),
            Value(Find(gpu, SensorType.Clock, "GPU Core", "GPU Shader")),
            Value(Find(gpu, SensorType.Power, "GPU Package", "GPU Power", "GPU Core")
                ?? Sensors(gpu, SensorType.Power).FirstOrDefault()),
            Max(Sensors(gpu, SensorType.Fan).Select(Value)),
            total,
            used,
            free,
            Value(Find(gpu, SensorType.Clock, "GPU Memory")),
            Value(Find(gpu, SensorType.Load, "GPU Memory Controller")));
    }

    private MemoryReading ReadMemory(IReadOnlyList<IHardware> dimms)
    {
        var status = PhysicalMemoryStatus.Query();

        // SPD の "DIMM #n" センサーのうち最も高い温度（しきい値系のセンサーは除外）
        double? temperature = Max(dimms
            .SelectMany(d => Sensors(d, SensorType.Temperature))
            .Where(s => s.Name.StartsWith("DIMM", StringComparison.OrdinalIgnoreCase))
            .Select(Value));

        double? installed = _modules.CapacityGb
            ?? (status is { } s ? Math.Ceiling(s.UsedGb + s.AvailableGb) : null);

        return new MemoryReading(
            status?.LoadPercent,
            status?.UsedGb,
            status?.AvailableGb,
            status?.TotalGb,
            temperature,
            installed,
            _modules.Type,
            _modules.SpeedMts);
    }

    /// <summary>ディスクリート GPU を優先し、同条件なら VRAM の大きいものを選ぶ。</summary>
    private static IHardware? SelectGpu(IEnumerable<IHardware> gpus) => gpus
        .OrderByDescending(h => h.Identifier.ToString().Contains("integrated", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
        .ThenByDescending(h => h.HardwareType switch
        {
            HardwareType.GpuNvidia => 3,
            HardwareType.GpuAmd => 2,
            _ => 1,
        })
        .ThenByDescending(h => Value(Find(h, SensorType.SmallData, "GPU Memory Total", "D3D Dedicated Memory Total")) ?? 0)
        .FirstOrDefault();

    private static bool IsGpu(IHardware h) =>
        h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;

    private static bool IsDimm(IHardware h) =>
        h.HardwareType == HardwareType.Memory
        && h.Identifier.ToString().Contains("/dimm/", StringComparison.OrdinalIgnoreCase);

    private static void UpdateRecursive(IHardware? hardware)
    {
        if (hardware is null)
            return;
        hardware.Update();
        foreach (var sub in hardware.SubHardware)
            UpdateRecursive(sub);
    }

    private static IEnumerable<ISensor> Sensors(IHardware hardware, SensorType type) =>
        hardware.Sensors.Concat(hardware.SubHardware.SelectMany(s => s.Sensors)).Where(s => s.SensorType == type);

    /// <summary>名前の候補を優先順に探す。</summary>
    private static ISensor? Find(IHardware? hardware, SensorType type, params string[] names)
    {
        if (hardware is null)
            return null;

        var sensors = Sensors(hardware, type).ToList();
        foreach (var name in names)
        {
            var match = sensors.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
        }
        return null;
    }

    private static double? Value(ISensor? sensor) =>
        sensor?.Value is float v && float.IsFinite(v) ? v : null;

    private static double? Max(IEnumerable<double?> values)
    {
        double? max = null;
        foreach (var v in values)
        {
            if (v is double d && (max is null || d > max))
                max = d;
        }
        return max;
    }
}
