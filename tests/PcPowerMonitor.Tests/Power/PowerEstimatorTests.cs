using PcPowerMonitor.Core.Hardware;
using PcPowerMonitor.Core.Models;
using PcPowerMonitor.Core.Power;

namespace PcPowerMonitor.Tests.Power;

public sealed class PowerEstimatorTests
{
    private readonly PowerEstimator _estimator = new();

    private static HardwareSnapshot Snapshot(CpuSnapshot? cpu, params GpuSnapshot[] gpus) => new(
        DateTimeOffset.UtcNow,
        SensorAvailability.Full,
        null,
        cpu,
        gpus,
        Memory: null,
        Disks: Array.Empty<DiskSnapshot>(),
        Fans: Array.Empty<FanSnapshot>(),
        Networks: Array.Empty<NetworkSnapshot>(),
        MainboardTempC: null);

    private static CpuSnapshot Cpu(double? power, double? load = null)
        => new("CPU", power, null, load, null);

    private static GpuSnapshot Gpu(double? power, double? load = null)
        => new("GPU", power, null, load, null, null, null);

    [Fact]
    public void Worked_example_cpu65_gpu150_gold_psu_650_gives_about_330w_measured()
    {
        // Use flat profile (idle = active) to isolate PowerEstimator logic from load-scaled baseline
        var profile = new HardwareProfile
        {
            PsuRating = PsuRating.Gold,
            SsdIdleW = 5,
            SsdActiveW = 5,
            MotherboardIdleW = 60,
            MotherboardW = 60,
            FanIdleW = 3,
            FanActiveW = 3,
        };

        var e = _estimator.Estimate(Snapshot(Cpu(65), Gpu(150)), profile);

        Assert.Equal(EstimationQuality.Measured, e.Quality);
        Assert.Equal(65d, e.CpuW, 3);
        Assert.Equal(150d, e.GpuW, 3);
        Assert.Equal(83d, e.BaselineW, 3);      // 2*3 + 1*5 + 0 + 3*3 + 60 + 3
        Assert.Equal(298d, e.DcTotalW, 3);
        Assert.InRange(e.PsuEfficiency, 0.89, 0.90);
        Assert.InRange(e.WallW, 328d, 337d);    // ~332.6 W
        Assert.Null(e.Notes);
    }

    [Fact]
    public void Cpu_measured_gpu_missing_falls_back_per_component_and_is_mixed()
    {
        var profile = new HardwareProfile { GpuTdpW = 200, PsuRating = PsuRating.Gold };

        var e = _estimator.Estimate(Snapshot(Cpu(65), Gpu(power: null, load: 50)), profile);

        Assert.Equal(EstimationQuality.Mixed, e.Quality);
        Assert.Equal(65d, e.CpuW, 3);
        Assert.Equal(100d, e.GpuW, 3);          // 200 W TDP * 50%
        Assert.Contains("GPU power sensor missing", e.Notes);
    }

    [Fact]
    public void No_cpu_power_but_load_present_uses_tdp_times_load()
    {
        var e = _estimator.Estimate(Snapshot(Cpu(power: null, load: 50)), new HardwareProfile());

        Assert.Equal(32.5d, e.CpuW, 3);         // 65 W TDP * 50%
        Assert.Equal(EstimationQuality.Estimated, e.Quality); // no dGPU + guessed CPU
        Assert.Contains("CPU power sensor missing", e.Notes);
    }

    [Fact]
    public void No_cpu_power_and_no_load_assumes_30_percent_and_marks_estimated()
    {
        var e = _estimator.Estimate(Snapshot(Cpu(power: null, load: null)), new HardwareProfile());

        Assert.Equal(65d * 0.30, e.CpuW, 3);
        Assert.Equal(EstimationQuality.Estimated, e.Quality);
        Assert.Contains("assumed TDP", e.Notes);
    }

    [Fact]
    public void Nan_cpu_power_is_treated_as_missing()
    {
        var e = _estimator.Estimate(Snapshot(Cpu(power: double.NaN, load: 50)), new HardwareProfile());

        Assert.Equal(32.5d, e.CpuW, 3);
        Assert.True(double.IsFinite(e.WallW));
    }

    [Fact]
    public void Psu_wattage_zero_uses_650_default_and_does_not_divide_by_zero()
    {
        var profile = new HardwareProfile { PsuWattage = 0 };

        var e = _estimator.Estimate(Snapshot(Cpu(65), Gpu(150)), profile);

        Assert.True(double.IsFinite(e.WallW));
        Assert.True(e.WallW > 0d);
        Assert.Contains("650", e.Notes);
    }

    [Fact]
    public void Out_of_range_profile_values_are_clamped_not_thrown()
    {
        // CpuTdpW clamps to 1000; power sensor absent so fallback uses the clamp.
        // Use flat profile to isolate from load-scaled baseline logic.
        var profile = new HardwareProfile
        {
            CpuTdpW = 999_999,
            RamSticks = 9_999,
            SsdIdleW = 5,
            SsdActiveW = 5,
            MotherboardIdleW = 60,
            MotherboardW = 60,
            FanIdleW = 3,
            FanActiveW = 3,
        };

        var e = _estimator.Estimate(Snapshot(Cpu(power: null, load: 100)), profile);

        Assert.Equal(1000d, e.CpuW, 3);
        // RAM sticks clamp to 32 → 32*3 + 1*5 + 3*3 + 60 + 3 = 173.
        Assert.Equal(173d, e.BaselineW, 3);
    }

    [Fact]
    public void No_discrete_gpu_and_measured_cpu_is_measured()
    {
        var e = _estimator.Estimate(Snapshot(Cpu(45)), new HardwareProfile());

        Assert.Equal(0d, e.GpuW, 3);
        Assert.Equal(EstimationQuality.Measured, e.Quality);
    }

    [Fact]
    public void Multiple_gpu_power_sensors_are_summed()
    {
        var e = _estimator.Estimate(Snapshot(Cpu(65), Gpu(30), Gpu(120)), new HardwareProfile());

        Assert.Equal(150d, e.GpuW, 3);
        Assert.Equal(EstimationQuality.Measured, e.Quality);
    }

    [Fact]
    public void Throws_on_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => _estimator.Estimate(null!, new HardwareProfile()));
        Assert.Throws<ArgumentNullException>(() => _estimator.Estimate(Snapshot(Cpu(10)), null!));
    }
}
