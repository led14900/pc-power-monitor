namespace PcPowerMonitor.Core.Models;

/// <summary>Immutable per-adapter throughput reading in KB/s, null when unknown.</summary>
public sealed record NetworkSnapshot(
    string? Name,
    double? UploadKBps,
    double? DownloadKBps);
