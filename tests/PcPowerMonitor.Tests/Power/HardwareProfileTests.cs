using PcPowerMonitor.Core.Power;

namespace PcPowerMonitor.Tests.Power;

public sealed class HardwareProfileTests
{
    [Fact]
    public void Ssd_idle_derived_from_active_when_sentinel()
    {
        var p = new HardwareProfile { SsdActiveW = 4, SsdIdleW = -1 }.Clamped();
        Assert.Equal(1d, p.SsdIdleW, 3); // 4 × 0.25
    }

    [Fact]
    public void Ssd_idle_explicit_value_is_kept()
    {
        var p = new HardwareProfile { SsdActiveW = 4, SsdIdleW = 2 }.Clamped();
        Assert.Equal(2d, p.SsdIdleW, 3);
    }

    [Fact]
    public void Motherboard_idle_derived_from_active_when_sentinel()
    {
        var p = new HardwareProfile { MotherboardW = 20, MotherboardIdleW = -1 }.Clamped();
        Assert.Equal(13d, p.MotherboardIdleW, 3); // 20 × 0.65
    }

    [Fact]
    public void Motherboard_idle_explicit_value_is_kept()
    {
        var p = new HardwareProfile { MotherboardW = 60, MotherboardIdleW = 39 }.Clamped();
        Assert.Equal(39d, p.MotherboardIdleW, 3);
    }

    [Fact]
    public void Fan_fields_use_explicit_defaults()
    {
        var p = new HardwareProfile().Clamped();
        Assert.Equal(0.5d, p.FanIdleW, 3);
        Assert.Equal(3.0d, p.FanActiveW, 3);
        Assert.Equal(2000d, p.FanMaxRpm, 3);
    }

    [Fact]
    public void Fan_idle_wattage_is_clamped_to_valid_range()
    {
        var p = new HardwareProfile { FanIdleW = -5 }.Clamped();
        Assert.InRange(p.FanIdleW, 0.1, 5);
    }

    [Fact]
    public void Fan_active_wattage_is_clamped_to_valid_range()
    {
        var p = new HardwareProfile { FanActiveW = 100 }.Clamped();
        Assert.InRange(p.FanActiveW, 0.5, 10);
    }

    [Fact]
    public void Fan_max_rpm_is_clamped_to_valid_range()
    {
        var p = new HardwareProfile { FanMaxRpm = 50000 }.Clamped();
        Assert.InRange(p.FanMaxRpm, 500, 10000);
    }

    [Fact]
    public void Ssd_active_is_clamped_to_valid_range()
    {
        var p = new HardwareProfile { SsdActiveW = 100 }.Clamped();
        Assert.InRange(p.SsdActiveW, 0.5, 15);
    }

    [Fact]
    public void Motherboard_active_is_clamped_to_valid_range()
    {
        var p = new HardwareProfile { MotherboardW = 2000 }.Clamped();
        Assert.InRange(p.MotherboardW, 0, 1000);
    }

    [Fact]
    public void Default_profile_clamped_equals_itself_after_derivation()
    {
        var p = new HardwareProfile();
        var clamped = p.Clamped();

        // After clamping, sentinel values should be derived
        Assert.NotEqual(p.SsdIdleW, clamped.SsdIdleW); // -1 → 0.875
        Assert.NotEqual(p.MotherboardIdleW, clamped.MotherboardIdleW); // -1 → 39
        Assert.Equal(p.FanIdleW, clamped.FanIdleW); // 0.5 stays
    }

    [Fact]
    public void Clamping_is_idempotent()
    {
        var p = new HardwareProfile { SsdActiveW = 10, MotherboardW = 100 };
        var clamped1 = p.Clamped();
        var clamped2 = clamped1.Clamped();

        // After clamping twice, values should be identical
        Assert.Equal(clamped1.SsdActiveW, clamped2.SsdActiveW);
        Assert.Equal(clamped1.SsdIdleW, clamped2.SsdIdleW);
        Assert.Equal(clamped1.MotherboardW, clamped2.MotherboardW);
        Assert.Equal(clamped1.MotherboardIdleW, clamped2.MotherboardIdleW);
        Assert.Equal(clamped1.FanIdleW, clamped2.FanIdleW);
        Assert.Equal(clamped1.FanActiveW, clamped2.FanActiveW);
    }
}
