using System.Collections.ObjectModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.Core.Models;
using PcPowerMonitor.Core.Sampling;
using PcPowerMonitor.Core.Settings;

namespace PcPowerMonitor.Core.Alerts;

/// <summary>
/// Subscribes to <see cref="ISnapshotBroadcaster.Ticked"/> and, per tick, checks CPU
/// temperature, hottest-GPU temperature and estimated wall power against the current
/// <see cref="AlertSettings"/>. Firing sends an <see cref="ITrayNotifier"/> notification,
/// appends to the capped <see cref="Log"/> and logs a warning. Cooldown + hysteresis
/// live in <see cref="AlertEvaluator"/>; this class owns only the wiring and the clock.
/// </summary>
public sealed class AlertService : IHostedService
{
    private const int MaxLogEntries = 50;

    private readonly ISnapshotBroadcaster _broadcaster;
    private readonly ISettingsStore _store;
    private readonly ITrayNotifier _notifier;
    private readonly ILogger<AlertService>? _log;

    private readonly AlertState _cpu = new();
    private readonly AlertState _gpu = new();
    private readonly AlertState _power = new();

    public AlertService(
        ISnapshotBroadcaster broadcaster,
        ISettingsStore store,
        ITrayNotifier notifier,
        ILogger<AlertService>? log = null)
    {
        _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _log = log;
    }

    /// <summary>Newest-first alert history. Bind with collection synchronisation on <see cref="LogSync"/>.</summary>
    public ObservableCollection<AlertLogEntry> Log { get; } = new();

    /// <summary>Lock object guarding <see cref="Log"/> for cross-thread WPF binding.</summary>
    public object LogSync { get; } = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _broadcaster.Ticked += OnTicked;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _broadcaster.Ticked -= OnTicked;
        return Task.CompletedTask;
    }

    private void OnTicked(SampleTick tick)
    {
        try
        {
            var alerts = _store.Current.Alerts;
            if (!alerts.Enabled) return;

            var now = DateTimeOffset.UtcNow;
            var cooldown = TimeSpan.FromMinutes(Math.Max(1, alerts.CooldownMinutes));

            var cpuTemp = tick.Snapshot.Cpu?.PackageTempC;
            if (AlertEvaluator.ShouldFire(cpuTemp, alerts.CpuTempC, _cpu, now, cooldown))
                Fire(AlertKind.CpuTemp, $"Nhiệt độ CPU {cpuTemp:0} °C vượt ngưỡng {alerts.CpuTempC:0} °C.");

            var gpuTemp = HottestGpu(tick.Snapshot.Gpus);
            if (AlertEvaluator.ShouldFire(gpuTemp, alerts.GpuTempC, _gpu, now, cooldown))
                Fire(AlertKind.GpuTemp, $"Nhiệt độ GPU {gpuTemp:0} °C vượt ngưỡng {alerts.GpuTempC:0} °C.");

            var wallW = tick.Estimate.WallW;
            if (AlertEvaluator.ShouldFire(wallW, alerts.PowerW, _power, now, cooldown))
                Fire(AlertKind.Power, $"Công suất ước tính {wallW:0} W vượt ngưỡng {alerts.PowerW:0} W.");
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Đánh giá cảnh báo thất bại.");
        }
    }

    private static double? HottestGpu(IReadOnlyList<GpuSnapshot>? gpus)
    {
        if (gpus is null || gpus.Count == 0) return null;
        double? max = null;
        foreach (var gpu in gpus)
            if (gpu.TempC is { } t && double.IsFinite(t))
                max = max is { } m ? Math.Max(m, t) : t;
        return max;
    }

    private void Fire(AlertKind kind, string message)
    {
        _log?.LogWarning("Cảnh báo {Kind}: {Message}", kind, message);

        try { _notifier.Notify("PC Power Monitor — Cảnh báo", message, "Warning"); }
        catch (Exception ex) { _log?.LogDebug(ex, "Gửi thông báo cảnh báo lỗi."); }

        var entry = new AlertLogEntry(DateTimeOffset.UtcNow, kind, message, "Warning");
        lock (LogSync)
        {
            Log.Insert(0, entry);
            while (Log.Count > MaxLogEntries) Log.RemoveAt(Log.Count - 1);
        }
    }
}
