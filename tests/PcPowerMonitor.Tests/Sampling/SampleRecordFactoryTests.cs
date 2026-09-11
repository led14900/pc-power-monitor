using PcPowerMonitor.Core.Hardware;
using PcPowerMonitor.Core.Models;
using PcPowerMonitor.Core.Power;
using PcPowerMonitor.Core.Sampling;

namespace PcPowerMonitor.Tests.Sampling;

public sealed class SampleRecordFactoryTests
{
    private static PowerEstimate Estimate() => new(
        CpuW: 45d, GpuW: 12d, BaselineW: 60d, DcTotalW: 117d,
        PsuEfficiency: 0.9d, WallW: 130d, Quality: EstimationQuality.Mixed, Notes: null);

    [Fact]
    public void Create_copies_estimate_energy_and_first_gpu_fields()
    {
        var ts = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        var snapshot = new HardwareSnapshot(
            ts, SensorAvailability.Full, null,
            Cpu: new CpuSnapshot("CPU", 45d, 62.5d, 33d, 4200d),
            Gpus: new[] { new GpuSnapshot("GPU", 12d, 55d, 20d, 1024d, 8192d, 1800d) },
            Memory: new MemorySnapshot(8d, 8d, 50d),
            Disks: Array.Empty<DiskSnapshot>(),
            Fans: Array.Empty<FanSnapshot>(),
            Networks: Array.Empty<NetworkSnapshot>(),
            MainboardTempC: 30d);
        var increment = new EnergyIncrement(0.00007d, 2d, false);

        var record = SampleRecordFactory.Create(snapshot, Estimate(), increment);

        Assert.Equal(ts.ToUnixTimeMilliseconds(), record.TsUtcMs);
        Assert.Equal(130d, record.WallW);
        Assert.Equal(45d, record.CpuW);
        Assert.Equal(12d, record.GpuW);
        Assert.Equal(117d, record.DcW);
        Assert.Equal(0.00007d, record.KwhDelta);
        Assert.Equal(2d, record.DtSeconds);
        Assert.False(record.IsGap);
        Assert.Equal((int)EstimationQuality.Mixed, record.Quality);
        Assert.Equal(62.5d, record.CpuTemp);
        Assert.Equal(55d, record.GpuTemp);
        Assert.Equal(33d, record.CpuLoad);
        Assert.Equal(20d, record.GpuLoad);
        Assert.Equal(50d, record.RamLoad);
    }

    [Fact]
    public void Create_leaves_sensor_fields_null_when_absent_and_flags_gap()
    {
        var snapshot = HardwareSnapshot.Empty(SensorAvailability.Unavailable, "no driver");
        var increment = EnergyIncrement.Gap(9999d);

        var record = SampleRecordFactory.Create(snapshot, Estimate(), increment);

        Assert.Null(record.CpuTemp);
        Assert.Null(record.GpuTemp);
        Assert.Null(record.CpuLoad);
        Assert.Null(record.GpuLoad);
        Assert.Null(record.RamLoad);
        Assert.True(record.IsGap);
        Assert.Equal(0d, record.KwhDelta);
    }
}
