namespace PcPowerMonitor.Core.Power;

/// <summary>
/// What one <see cref="EnergyAccumulator.Add"/> call contributed: the energy added
/// this step, the measured time delta, and whether the step was discarded as a gap
/// (sleep / hibernate / process pause). On a gap, <see cref="Kwh"/> is 0.
/// </summary>
public readonly record struct EnergyIncrement(double Kwh, double DtSeconds, bool GapDetected)
{
    public static EnergyIncrement Zero(double dtSeconds) => new(0d, dtSeconds, false);

    public static EnergyIncrement Gap(double dtSeconds) => new(0d, dtSeconds, true);
}
