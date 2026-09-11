namespace PcPowerMonitor.Core.Power;

/// <summary>
/// How trustworthy a <see cref="PowerEstimate"/> is. Stored per sample so reports
/// can flag which stretches lean on TDP guesses instead of real sensors.
/// </summary>
public enum EstimationQuality
{
    /// <summary>Both CPU and GPU power came from real sensors.</summary>
    Measured,

    /// <summary>One of CPU / GPU was sensor-read, the other fell back to TDP x load%.</summary>
    Mixed,

    /// <summary>Neither CPU nor GPU had a usable power sensor.</summary>
    Estimated,
}
