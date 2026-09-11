namespace PcPowerMonitor.Core.Hardware;

/// <summary>
/// How much of the hardware sensor surface the app could actually read.
/// UI and power estimation behave differently at each level.
/// </summary>
public enum SensorAvailability
{
    /// <summary>LibreHardwareMonitor could not be opened at all (no driver / no admin).</summary>
    Unavailable = 0,

    /// <summary>Temperatures / loads readable, but no CPU/GPU power sensor — fall back to TDP x Load.</summary>
    Partial = 1,

    /// <summary>At least one CPU or GPU power sensor is present.</summary>
    Full = 2,
}
