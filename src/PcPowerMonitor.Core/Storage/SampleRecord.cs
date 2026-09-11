namespace PcPowerMonitor.Core.Storage;

/// <summary>
/// One row destined for <c>SensorSamples</c>. <paramref name="TsUtcMs"/> is Unix epoch
/// milliseconds UTC. <paramref name="KwhDelta"/> comes straight from
/// <c>EnergyIncrement.Kwh</c> (0 on a gap) — rollups sum these, never <c>avg_w x time</c>.
/// </summary>
public readonly record struct SampleRecord(
    long TsUtcMs,
    double WallW,
    double? CpuW,
    double? GpuW,
    double? DcW,
    double KwhDelta,
    double DtSeconds,
    bool IsGap,
    int Quality,
    double? CpuTemp,
    double? GpuTemp,
    double? CpuLoad,
    double? GpuLoad,
    double? RamLoad);
