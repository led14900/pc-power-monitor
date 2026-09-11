using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.Core.Storage;

namespace PcPowerMonitor.Core.Sampling;

/// <summary>
/// Background housekeeping, separate from the sampling loop so a slow rollup never
/// stalls a sensor read. Runs <see cref="RollupService"/> every 5 minutes (plus once
/// at start, to catch up for the time the app was off) and <see cref="RetentionService"/>
/// once a day. Each pass is independently wrapped so one failure never stops the other.
/// </summary>
public sealed class MaintenanceHostedService : BackgroundService
{
    private static readonly TimeSpan RollupInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RetentionInterval = TimeSpan.FromHours(24);

    private readonly RollupService _rollup;
    private readonly RetentionService _retention;
    private readonly ILogger<MaintenanceHostedService>? _log;

    public MaintenanceHostedService(
        RollupService rollup,
        RetentionService retention,
        ILogger<MaintenanceHostedService>? log = null)
    {
        _rollup = rollup ?? throw new ArgumentNullException(nameof(rollup));
        _retention = retention ?? throw new ArgumentNullException(nameof(retention));
        _log = log;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rollupLoop = RunLoopAsync("rollup", RollupInterval, runImmediately: true,
            ct => _rollup.RunAsync(ct), stoppingToken);
        var retentionLoop = RunLoopAsync("retention", RetentionInterval, runImmediately: false,
            ct => _retention.RunAsync(ct), stoppingToken);
        return Task.WhenAll(rollupLoop, retentionLoop);
    }

    private async Task RunLoopAsync(
        string name,
        TimeSpan interval,
        bool runImmediately,
        Func<CancellationToken, Task> action,
        CancellationToken stoppingToken)
    {
        if (runImmediately)
            await SafeRunAsync(name, action, stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                await SafeRunAsync(name, action, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task SafeRunAsync(string name, Func<CancellationToken, Task> action, CancellationToken ct)
    {
        try
        {
            await action(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Tác vụ bảo trì '{Name}' thất bại.", name);
        }
    }
}
