namespace PcPowerMonitor.Core.Sampling;

/// <summary>
/// Mutable, process-wide holder for sampling-loop settings. Registered as a singleton
/// so the tray menu ("Tạm dừng ghi") and the Settings screen (phase 08) can flip
/// <see cref="Paused"/> / change <see cref="IntervalSeconds"/> and the running
/// <c>SamplingHostedService</c> picks it up on its next tick.
/// </summary>
public sealed class SamplingOptions
{
    /// <summary>Inclusive lower bound for <see cref="IntervalSeconds"/>.</summary>
    public const int MinIntervalSeconds = 1;

    /// <summary>Inclusive upper bound for <see cref="IntervalSeconds"/>.</summary>
    public const int MaxIntervalSeconds = 60;

    private int _intervalSeconds = 2;

    /// <summary>Seconds between sensor reads. Clamped to [1, 60]; default 2.</summary>
    public int IntervalSeconds
    {
        get => _intervalSeconds;
        set => _intervalSeconds = Math.Clamp(value, MinIntervalSeconds, MaxIntervalSeconds);
    }

    /// <summary>When true the loop still ticks but only calls <c>EnergyAccumulator.MarkGap()</c>.</summary>
    public bool Paused { get; set; }

    /// <summary><see cref="IntervalSeconds"/> as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan Interval => TimeSpan.FromSeconds(_intervalSeconds);
}
