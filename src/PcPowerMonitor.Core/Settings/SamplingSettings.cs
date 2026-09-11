namespace PcPowerMonitor.Core.Settings;

/// <summary>
/// Sampling-loop configuration. <see cref="IntervalSeconds"/> is pushed into the
/// process-wide <c>SamplingOptions</c> singleton on every settings change; the pause
/// state is deliberately NOT here — it is owned by the tray menu.
/// </summary>
public sealed record SamplingSettings
{
    /// <summary>Seconds between sensor reads. Valid range 1-60; default 2.</summary>
    public int IntervalSeconds { get; init; } = 2;
}
