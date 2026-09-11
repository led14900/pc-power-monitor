namespace PcPowerMonitor.Core.Models;

/// <summary>
/// Immutable per-GPU reading. Laptops may report two GPUs (iGPU + dGPU); callers
/// treat this as a list and skip entries whose <see cref="PowerW"/> is null.
/// </summary>
public sealed record GpuSnapshot(
    string? Name,
    double? PowerW,
    double? TempC,
    double? CoreLoadPercent,
    double? MemoryUsedMb,
    double? MemoryTotalMb,
    double? CoreClockMhz);
