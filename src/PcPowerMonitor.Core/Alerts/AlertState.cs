namespace PcPowerMonitor.Core.Alerts;

/// <summary>
/// Per-<see cref="AlertKind"/> mutable state for <see cref="AlertEvaluator"/>:
/// when it last fired and whether it is "armed" (the value has dropped back below the
/// hysteresis band since the last fire). One instance per kind, owned by the service.
/// </summary>
public sealed class AlertState
{
    /// <summary>UTC time of the most recent fire, or null if it has never fired.</summary>
    public DateTimeOffset? LastFiredUtc { get; set; }

    /// <summary>True when a fresh crossing is allowed. Cleared on fire, set again by hysteresis.</summary>
    public bool Armed { get; set; } = true;
}
