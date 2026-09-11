namespace PcPowerMonitor.Core.Models;

/// <summary>Immutable per-drive reading. <see cref="Name"/> is model only (serials scrubbed).</summary>
public sealed record DiskSnapshot(
    string? Name,
    double? TempC,
    double? ActivityPercent,
    double? UsedPercent);
