namespace PcPowerMonitor.Core.Power;

/// <summary>
/// Fixed "everything except CPU and GPU" draw derived from the <see cref="HardwareProfile"/>:
/// RAM + SSDs + HDDs + fans + motherboard + peripherals. Per-part watt figures are
/// mid-range load values from the phase-01 research.
/// </summary>
public static class BaselinePowerCalculator
{
    private const double SsdWatt = 5d;
    private const double HddWatt = 8d;
    private const double FanWatt = 3d;
    private const double Ddr4WattPerStick = 3d;
    private const double Ddr5WattPerStick = 4d;

    /// <summary>Baseline watts for the profile. Input is clamped first, so the result is always finite.</summary>
    public static double Calculate(HardwareProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var p = profile.Clamped();

        var ramWattPerStick = string.Equals(p.RamType, "DDR5", StringComparison.OrdinalIgnoreCase)
            ? Ddr5WattPerStick
            : Ddr4WattPerStick;

        return p.RamSticks * ramWattPerStick
             + p.SsdCount * SsdWatt
             + p.HddCount * HddWatt
             + p.FanCount * FanWatt
             + p.MotherboardW
             + p.PeripheralW;
    }
}
