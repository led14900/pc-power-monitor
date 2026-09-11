using PcPowerMonitor.Core.Hardware;

namespace PcPowerMonitor.Core.Models;

/// <summary>
/// One immutable point-in-time reading of the whole machine. Produced by
/// <c>IHardwareSensorReader.ReadSnapshot()</c>, consumed by the power estimator,
/// the sample writer and the dashboard.
/// </summary>
public sealed record HardwareSnapshot(
    DateTimeOffset TimestampUtc,
    SensorAvailability Availability,
    string? AvailabilityReason,
    CpuSnapshot? Cpu,
    IReadOnlyList<GpuSnapshot> Gpus,
    MemorySnapshot? Memory,
    IReadOnlyList<DiskSnapshot> Disks,
    IReadOnlyList<FanSnapshot> Fans,
    IReadOnlyList<NetworkSnapshot> Networks,
    double? MainboardTempC)
{
    /// <summary>An all-null snapshot carrying the current availability + reason.</summary>
    public static HardwareSnapshot Empty(SensorAvailability availability, string? reason) => new(
        DateTimeOffset.UtcNow,
        availability,
        reason,
        Cpu: null,
        Gpus: Array.Empty<GpuSnapshot>(),
        Memory: null,
        Disks: Array.Empty<DiskSnapshot>(),
        Fans: Array.Empty<FanSnapshot>(),
        Networks: Array.Empty<NetworkSnapshot>(),
        MainboardTempC: null);
}
