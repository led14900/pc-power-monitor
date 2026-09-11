namespace PcPowerMonitor.Core.Models;

/// <summary>Immutable system RAM reading. Values in GB / percent, null when unknown.</summary>
public sealed record MemorySnapshot(
    double? UsedGb,
    double? AvailableGb,
    double? LoadPercent);
