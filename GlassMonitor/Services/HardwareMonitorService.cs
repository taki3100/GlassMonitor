using System.Diagnostics;

namespace GlassMonitor.Services;

/// <summary>
/// バックグラウンドで 1 秒ごとにセンサーを読み取り、<see cref="SnapshotAvailable"/> で通知する。
/// LibreHardwareMonitor の操作はすべてこのワーカー上で行う。
/// </summary>
/// <param name="demoDisks">--demo-disks の台数（通常は null）。</param>
public sealed class HardwareMonitorService(bool memoryTemperature, int? demoDisks = null) : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    private readonly CancellationTokenSource _cts = new();
    private volatile bool _memoryTemperature = memoryTemperature;
    private volatile IReadOnlyCollection<string> _selectedDrives = [];
    private Task? _worker;

    /// <summary>ワーカースレッドから呼ばれる。</summary>
    public event Action<HardwareSnapshot>? SnapshotAvailable;

    /// <summary>メモリ温度（SMBus 経由の SPD 読み取り）の ON/OFF。次の更新から反映される。</summary>
    public bool MemoryTemperatureEnabled
    {
        get => _memoryTemperature;
        set => _memoryTemperature = value;
    }

    /// <summary>5 台以上あるときに表示するディスクのキー。次の更新から反映される。</summary>
    public IReadOnlyCollection<string> SelectedDrives
    {
        get => _selectedDrives;
        set => _selectedDrives = value.ToArray();
    }

    public void Start() => _worker ??= Task.Run(() => RunAsync(_cts.Token));

    private async Task RunAsync(CancellationToken ct)
    {
        using var reader = new SensorReader(_memoryTemperature, demoDisks);
        try
        {
            // PawnIO ドライバーの読み込みなどで数秒かかることがある
            reader.Open();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LibreHardwareMonitor の初期化に失敗しました: {ex}");
        }

        using var timer = new PeriodicTimer(Interval);
        try
        {
            do
            {
                try
                {
                    reader.SetMemoryTemperatureEnabled(_memoryTemperature);
                    SnapshotAvailable?.Invoke(reader.Read(_selectedDrives));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"センサーの読み取りに失敗しました: {ex}");
                }
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _worker?.Wait(TimeSpan.FromSeconds(3));
        }
        catch (AggregateException)
        {
        }
        _cts.Dispose();
    }
}
