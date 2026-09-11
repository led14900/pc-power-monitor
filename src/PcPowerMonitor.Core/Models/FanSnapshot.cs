namespace PcPowerMonitor.Core.Models;

/// <summary>Immutable fan reading. <see cref="Rpm"/> null when the channel reports nothing.</summary>
public sealed record FanSnapshot(
    string? Name,
    double? Rpm);
