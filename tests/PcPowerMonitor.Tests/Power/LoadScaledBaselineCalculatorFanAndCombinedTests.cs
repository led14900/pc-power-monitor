using PcPowerMonitor.Core.Hardware;
using PcPowerMonitor.Core.Models;
using PcPowerMonitor.Core.Power;

namespace PcPowerMonitor.Tests.Power;

public sealed class LoadScaledBaselineCalculatorFanAndCombinedTests
{
    private static HardwareSnapshot Snapshot(
        CpuSnapshot? cpu = null,
        DiskSnapshot[]? disks = null,
        FanSnapshot[]? fans = null) => new(
        DateTimeOffset.UtcNow,
        SensorAvailability.Full,
        null,
        cpu,
        Array.Empty<GpuSnapshot>(),
        null,
        disks ?? Array.Empty<DiskSnapshot>(),
        fans ?? Array.Empty<FanSnapshot>(),
        Array.Empty<NetworkSnapshot>(),
        null);

    private static CpuSnapshot Cpu(double? load)
        => new("CPU", null, null, load, null);

    private static DiskSnapshot Disk(double? activity)
        => new("SSD", null, activity, null);

    private static FanSnapshot Fan(double? rpm)
        => new("Fan", rpm);

    // Fan tests (cubic scaling)
    [Fact]
    public void Fan_at_zero_rpm_uses_idle_wattage()
    {
        var profile = new HardwareProfile { FanIdleW = 0.5, FanActiveW = 3, FanMaxRpm = 2000 };
        var snapshot = Snapshot(fans: new[] { Fan(0) });

        var result = LoadScaledBaselineCalculator.Calculate(snapshot, profile.Clamped());

        // Fan: Lerp(0.5, 3, 0³/1) = 0.5W
        var expected = 6 + 2.1875 + 0 + 0.5 + 45.3 + 3;
        Assert.Equal(expected, result, 2);
    }

    [Fact]
    public void Fan_at_max_rpm_uses_active_wattage()
    {
        var profile = new HardwareProfile { FanIdleW = 0.5, FanActiveW = 3, FanMaxRpm = 2000 };
        var snapshot = Snapshot(fans: new[] { Fan(2000) });

        var result = LoadScaledBaselineCalculator.Calculate(snapshot, profile.Clamped());

        // Fan: Lerp(0.5, 3, 1³) = 3.0W
        var expected = 6 + 2.1875 + 0 + 3.0 + 45.3 + 3;
        Assert.Equal(expected, result, 2);
    }

    [Fact]
    public void Fan_at_half_rpm_uses_cubic_interpolation()
    {
        var profile = new HardwareProfile { FanIdleW = 0.5, FanActiveW = 3, FanMaxRpm = 2000 };
        var snapshot = Snapshot(fans: new[] { Fan(1000) }); // 50% RPM

        var result = LoadScaledBaselineCalculator.Calculate(snapshot, profile.Clamped());

        // Fan: Lerp(0.5, 3, 0.5³) = 0.5 + 2.5 * 0.125 = 0.8125W
        var expected = 6 + 2.1875 + 0 + 0.8125 + 45.3 + 3;
        Assert.Equal(expected, result, 2);
    }

    [Fact]
    public void Fan_at_half_rpm_proves_cubic_not_linear()
    {
        var profile = new HardwareProfile { FanIdleW = 0.5, FanActiveW = 3, FanMaxRpm = 2000 };
        var snapshot = Snapshot(fans: new[] { Fan(1000) }); // 50% RPM

        var result = LoadScaledBaselineCalculator.Calculate(snapshot, profile.Clamped());
        // Fan only contribution: 0.8125W
        var fanOnly = result - 6 - 2.1875 - 45.3 - 3; // subtract non-fan parts

        // Cubic: 0.5³ = 0.125 → 0.5 + 2.5*0.125 = 0.8125W (not 1.75W linear)
        Assert.Equal(0.8125, fanOnly, 2);
    }

    [Fact]
    public void Fan_null_rpm_uses_midpoint()
    {
        var profile = new HardwareProfile { FanIdleW = 0.5, FanActiveW = 3 };
        var snapshot = Snapshot(fans: new[] { Fan(null) });

        var result = LoadScaledBaselineCalculator.Calculate(snapshot, profile.Clamped());

        // Fan: (0.5 + 3) / 2 = 1.75W
        var expected = 6 + 2.1875 + 0 + 1.75 + 45.3 + 3;
        Assert.Equal(expected, result, 2);
    }

    [Fact]
    public void No_fans_falls_back_to_fan_count_midpoint()
    {
        var profile = new HardwareProfile { FanCount = 3, FanIdleW = 0.5, FanActiveW = 3 };
        var snapshot = Snapshot(fans: Array.Empty<FanSnapshot>());

        var result = LoadScaledBaselineCalculator.Calculate(snapshot, profile.Clamped());

        // Fallback: 3 × (0.5 + 3) / 2 = 5.25W
        var expected = 6 + 2.1875 + 0 + 5.25 + 45.3 + 3;
        Assert.Equal(expected, result, 2);
    }

    // Combined tests
    [Fact]
    public void Combined_baseline_idle_all_zero_activity()
    {
        var profile = new HardwareProfile().Clamped();
        var snapshot = Snapshot(cpu: Cpu(0), disks: new[] { Disk(0) }, fans: new[] { Fan(0) });

        var result = LoadScaledBaselineCalculator.Calculate(snapshot, profile);

        // All idle: RAM 6 + SSD 0.875 + HDD 0 + Fan 0.5 + MB 39 + Peri 3 = 49.375W
        Assert.Equal(49.375, result, 2);
    }

    [Fact]
    public void Combined_baseline_active_all_hundred_percent()
    {
        var profile = new HardwareProfile().Clamped();
        var snapshot = Snapshot(cpu: Cpu(100), disks: new[] { Disk(100) }, fans: new[] { Fan(2000) });

        var result = LoadScaledBaselineCalculator.Calculate(snapshot, profile);

        // All active: RAM 6 + SSD 3.5 + HDD 0 + Fan 3.0 + MB 60 + Peri 3 = 75.5W
        Assert.Equal(75.5, result, 2);
    }

    [Fact]
    public void Hdd_detected_by_live_snapshot_is_not_double_counted()
    {
        // LHM's Storage hardware type has no drive-type flag, so a physically-present
        // HDD shows up in snapshot.Disks alongside SSDs. Once a live disk is reported,
        // the flat HddCount x HddWatt term must not also apply to that same drive.
        var profile = new HardwareProfile { SsdCount = 0, HddCount = 1, SsdActiveW = 4, SsdIdleW = 1 };
        var snapshot = Snapshot(disks: new[] { Disk(50) }); // the HDD, reported with an activity%

        var result = LoadScaledBaselineCalculator.Calculate(snapshot, profile.Clamped());

        // Only the per-device lerp applies (Lerp(1, 4, 0.5) = 2.5W); the flat 8W HDD
        // term must be suppressed because live disk data is present.
        var expected = 6 + 2.5 + 0 + 5.25 + 45.3 + 3;
        Assert.Equal(expected, result, 2);
    }

    [Fact]
    public void Combined_baseline_mid_load_variations()
    {
        var profile = new HardwareProfile().Clamped();
        var snapshot = Snapshot(
            cpu: Cpu(50),
            disks: new[] { Disk(50) },
            fans: new[] { Fan(1000) }); // 50% RPM

        var result = LoadScaledBaselineCalculator.Calculate(snapshot, profile);

        // Mid-load:
        // SSD 50%: Lerp(0.875, 3.5, 0.5) = 2.1875W
        // MB 50%: Lerp(39, 60, 0.5) = 49.5W
        // Fan 50% (cubic): Lerp(0.5, 3.0, 0.125) = 0.8125W
        // Total: 6 + 2.1875 + 0 + 0.8125 + 49.5 + 3 = 61.5W
        Assert.Equal(61.5, result, 1);
    }
}
