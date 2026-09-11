using PcPowerMonitor.Core.Models;
using PcPowerMonitor.Core.Power;
using PcPowerMonitor.Core.Storage;

namespace PcPowerMonitor.Core.Sampling;

/// <summary>
/// Pure mapping from a produced sample to its persisted <see cref="SampleRecord"/> row.
/// Kept out of the hosted service so it stays small and this stays unit-testable.
/// Nullable sensor fields are passed through untouched — never coerced to 0.
/// </summary>
public static class SampleRecordFactory
{
    public static SampleRecord Create(HardwareSnapshot snapshot, PowerEstimate estimate, EnergyIncrement increment)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(estimate);

        var firstGpu = snapshot.Gpus.Count > 0 ? snapshot.Gpus[0] : null;

        return new SampleRecord(
            TsUtcMs: snapshot.TimestampUtc.ToUnixTimeMilliseconds(),
            WallW: estimate.WallW,
            CpuW: estimate.CpuW,
            GpuW: estimate.GpuW,
            DcW: estimate.DcTotalW,
            KwhDelta: increment.Kwh,
            DtSeconds: increment.DtSeconds,
            IsGap: increment.GapDetected,
            Quality: (int)estimate.Quality,
            CpuTemp: snapshot.Cpu?.PackageTempC,
            GpuTemp: firstGpu?.TempC,
            CpuLoad: snapshot.Cpu?.TotalLoadPercent,
            GpuLoad: firstGpu?.CoreLoadPercent,
            RamLoad: snapshot.Memory?.LoadPercent);
    }
}
