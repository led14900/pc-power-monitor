using PcPowerMonitor.Core.Power;

namespace PcPowerMonitor.Tests.Power;

public sealed class BaselinePowerCalculatorTests
{
    [Fact]
    public void Default_profile_baseline_is_83_watts()
    {
        // 2 DDR4 sticks*3 + 1 SSD*5 + 0 HDD*8 + 3 fans*3 + 60 board + 3 peripheral.
        Assert.Equal(83d, BaselinePowerCalculator.Calculate(new HardwareProfile()), 6);
    }

    [Fact]
    public void Ddr5_costs_one_more_watt_per_stick()
    {
        var p = new HardwareProfile { RamType = "ddr5" }; // case-insensitive
        Assert.Equal(85d, BaselinePowerCalculator.Calculate(p), 6);
    }

    [Fact]
    public void Hdds_are_eight_watts_each()
    {
        var p = new HardwareProfile { HddCount = 2 };
        Assert.Equal(83d + 16d, BaselinePowerCalculator.Calculate(p), 6);
    }

    [Fact]
    public void Counts_are_clamped_to_32()
    {
        var p = new HardwareProfile { SsdCount = 999 };
        // 6 ram + 32*5 ssd + 9 fan + 60 board + 3 peripheral = 238.
        Assert.Equal(238d, BaselinePowerCalculator.Calculate(p), 6);
    }

    [Fact]
    public void Negative_board_watts_are_clamped_to_zero()
    {
        var p = new HardwareProfile { MotherboardW = -50, PeripheralW = 0 };
        // 6 ram + 5 ssd + 9 fan + 0 board + 0 peripheral = 20.
        Assert.Equal(20d, BaselinePowerCalculator.Calculate(p), 6);
    }

    [Fact]
    public void Throws_on_null_profile()
        => Assert.Throws<ArgumentNullException>(() => BaselinePowerCalculator.Calculate(null!));
}
