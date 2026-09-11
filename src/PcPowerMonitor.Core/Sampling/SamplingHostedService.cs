using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.Core.Hardware;
using PcPowerMonitor.Core.Power;
using PcPowerMonitor.Core.Settings;
using PcPowerMonitor.Core.Storage;

namespace PcPowerMonitor.Core.Sampling;

/// <summary>
/// The heartbeat: every <see cref="SamplingOptions.IntervalSeconds"/> it reads sensors,
/// estimates power, folds energy into the accumulator, queues a DB row and broadcasts
/// a <see cref="SampleTick"/>. A <see cref="PeriodicTimer"/> is used so a slow read
/// never drifts or re-enters. One tick failing never kills the loop.
/// </summary>
public sealed class SamplingHostedService : BackgroundService
{
    private const int MaxConsecutiveErrors = 10;
    private static readonly TimeSpan BackoffDelay = TimeSpan.FromSeconds(60);

    private readonly IHardwareSensorReader _reader;
    private readonly PowerEstimator _estimator;
    private readonly EnergyAccumulator _accumulator;
    private readonly ISampleWriter _writer;
    private readonly ISnapshotBroadcaster _broadcaster;
    private readonly IEnergyQueryService _query;
    private readonly SamplingOptions _options;
    private readonly ISettingsStore _store;
    private readonly ILogger<SamplingHostedService>? _log;

    private int _consecutiveErrors;

    public SamplingHostedService(
        IHardwareSensorReader reader,
        PowerEstimator estimator,
        EnergyAccumulator accumulator,
        ISampleWriter writer,
        ISnapshotBroadcaster broadcaster,
        IEnergyQueryService query,
        SamplingOptions options,
        ISettingsStore store,
        ILogger<SamplingHostedService>? log = null)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _estimator = estimator ?? throw new ArgumentNullException(nameof(estimator));
        _accumulator = accumulator ?? throw new ArgumentNullException(nameof(accumulator));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
        _query = query ?? throw new ArgumentNullException(nameof(query));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _log = log;

        // Settings are the single source for the sample interval. Seed it now and keep
        // it in sync on every change; the loop re-reads _options.Interval each tick.
        // SamplingOptions.Paused is owned by the tray and deliberately left untouched.
        _options.IntervalSeconds = _store.Current.Sampling.IntervalSeconds;
        _store.Changed += OnSettingsChanged;
    }

    private void OnSettingsChanged(AppSettings settings)
        => _options.IntervalSeconds = settings.Sampling.IntervalSeconds;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        SeedAccumulator();
        _accumulator.SetExpectedInterval(_options.IntervalSeconds);

        var period = _options.Interval;
        using var timer = new PeriodicTimer(period);
        _log?.LogInformation("Sampling loop started at {Seconds}s interval.", _options.IntervalSeconds);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                if (_options.Interval != period)
                {
                    period = _options.Interval;
                    timer.Period = period;
                    _accumulator.SetExpectedInterval(_options.IntervalSeconds);
                    _log?.LogInformation("Sampling interval changed to {Seconds}s.", _options.IntervalSeconds);
                }

                if (_options.Paused) { _accumulator.MarkGap(); continue; }

                try
                {
                    DoTick();
                    _consecutiveErrors = 0;
                }
                catch (Exception ex)
                {
                    _log?.LogError(ex, "Lỗi khi lấy mẫu (lần liên tiếp {Count}).", _consecutiveErrors + 1);
                    if (++_consecutiveErrors > MaxConsecutiveErrors)
                    {
                        _log?.LogError("Quá {Max} lỗi liên tiếp — tạm dừng {Seconds}s.",
                            MaxConsecutiveErrors, BackoffDelay.TotalSeconds);
                        _accumulator.MarkGap();
                        await Task.Delay(BackoffDelay, stoppingToken).ConfigureAwait(false);
                        _consecutiveErrors = 0;
                    }
                }
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
    }

    private void SeedAccumulator()
    {
        try { _accumulator.Reset(_query.GetLastTotalKwh()); }
        catch (Exception ex) { _log?.LogError(ex, "Không đọc được tổng kWh đã lưu; bắt đầu từ 0."); }
        _accumulator.MarkGap(); // no prior reference point right after startup
    }

    private void DoTick()
    {
        var snapshot = _reader.ReadSnapshot();
        // Immutable record; a per-tick read is cheap and always sees the latest profile.
        var profile = _store.Current.HardwareProfile.Clamped();
        var estimate = _estimator.Estimate(snapshot, profile);
        var increment = _accumulator.Add(estimate.WallW);

        _writer.Enqueue(SampleRecordFactory.Create(snapshot, estimate, increment));
        _broadcaster.Publish(new SampleTick(snapshot, estimate, increment));
    }

    public override void Dispose()
    {
        _store.Changed -= OnSettingsChanged;
        base.Dispose();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Flush cuối cùng thất bại khi dừng sampling.");
        }

        try
        {
            _reader.Dispose();
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Dispose sensor reader lỗi khi dừng.");
        }
    }
}
