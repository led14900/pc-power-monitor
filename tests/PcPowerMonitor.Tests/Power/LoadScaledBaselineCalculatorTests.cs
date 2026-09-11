using PcPowerMonitor.Core.Hardware;
using PcPowerMonitor.Core.Models;
using PcPowerMonitor.Core.Power;

namespace PcPowerMonitor.Tests.Power;

public sealed class LoadScaledBaselineCalculatorTests
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

    // SSD tests
    [Fact]
    public void Ssd_at_zero_activity_uses_idle_wattage()
    {
        var profile = new HardwareProfile { SsdActiveW = 4, SsdIdleW = 1 };
        var snapshot = Snapshot(disks: new[] { Disk(0) });

        var result = LoadScaledBaselineCalculator.Calculate(snapshot, profile.Clamped());

        // SSD: Lerp(1, 4, 0/100) = 1W (idle)
        // Others: RAM 6 + MB (30% CPU) 39+0.3*21=45.3 + Fan midpoint 1.75 + Peripheral 3
        var expected = 6 + 1 + 0 + 5.25 + 45.3 + 3;
        Assert.Equal(expected, result, 2);
    }

    [Fact]
    public void Ssd_at_full_activity_uses_active_wattage()
    {
        var profile = new HardwareProfile { SsdActiveW = 4, SsdIdleW = 1 };
        var snapshot = Snapshot(disks: new[] { Disk(100) });

        var result = LoadScaledBaselineCalculator.Calculate(snapshot, profile.Clamped());

        // SSD: Lerp(1, 4, 100/100) = 4W (active)
        var expected = 6 + 4 + 0 + 5.25 + 45.3 + 3;
        Assert.Equal(expected, result, 2);
    }

    [Fact]
    public void Multiple_ssds_sum_independently()
    {
        var profile = new HardwareProfile { SsdActiveW = 4, SsdIdleW = 1, SsdCount = 2 };
        var snapshot = Snapshot(disks: new[] { Disk(0), Disk(100) });

        var result = LoadScaledBaselineCalculator.Calculate(snapshot, profile.Clamped());

        // SSD: 1 + 4 = 5W total
        var expected = 6 + 5 + 0 + 5.25 + 45.3 + 3;
        Assert.Equal(expected, result, 2);
    }

    [Fact]
    public void No_disks_falls_back_to_ssd_count_midpoint()
    {
        var profile = new HardwareProfile { SsdCount = 1, SsdActiveW = 4, SsdIdleW = 1 };
        var snapshot = Snapshot(disks: Array.Empty<DiskSnapshot>());

        var result = LoadScaledBaselineCalculator.Calculate(snapshot, profile.Clamped());

        // Fallback: 1 × (1 + 4) / 2 = 2.5W
        var expected = 6 + 2.5 + 0 + 5.25 + 45.3 + 3;
        Assert.Equal(expected, result, 2);
    }

    // Mainboard tests
    [Fact]
    public void Mainboard_at_zero_cpu_load_uses_idle_wattage()
    {
        var profile = new HardwareProfile { MotherboardW = 60, MotherboardIdleW = 39 };
        var snapshot = Snapshot(cpu: Cpu(0));

        var result = LoadScaledBaselineCalculator.Calculate(snapshot, profile.Clamped());

        // MB: Lerp(39, 60, 0/100) = 39W (idle)
        var expected = 6 + 2.1875 + 0 + 5.25 + 39 + 3;
        Assert.Equal(expected, result, 2);
    }

    [Fact]
    public void Mainboard_at_full_cpu_load_uses_active_wattage()
    {
        var profile = new HardwareProfile { MotherboardW = 60, MotherboardIdleW = 39 };
        var snapshot = Snapshot(cpu: Cpu(100));

        var result = LoadScaledBaselineCalculator.Calculate(snapshot, profile.Clamped());

        // MB: Lerp(39, 60, 100/100) = 60W (active)
        var expected = 6 + 2.1875 + 0 + 5.25 + 60 + 3;
        Assert.Equal(expected, result, 2);
    }

    [Fact]
    public void Null_cpu_defaults_to_30_percent_load()
    {
        var profile = new HardwareProfile { MotherboardW = 60, MotherboardIdleW = 39 };
        var snapshot = Snapshot(cpu: null);

        var result = LoadScaledBaselineCalculator.Calculate(snapshot, profile.Clamped());

        // MB: Lerp(39, 60, 0.30) = 39 + 21*0.30 = 45.3W
        var expected = 6 + 2.1875 + 0 + 5.25 + 45.3 + 3;
        Assert.Equal(expected, result, 2);
    }

    [Fact]
    public void Throws_on_null_snapshot()
        => Assert.Throws<ArgumentNullException>(() =>
            LoadScaledBaselineCalculator.Calculate(null!, new HardwareProfile()));

    [Fact]
    public void Throws_on_null_profile()
        => Assert.Throws<ArgumentNullException>(() =>
            LoadScaledBaselineCalculator.Calculate(Snapshot(), null!));
}
