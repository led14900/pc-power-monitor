namespace PcPowerMonitor.Core.Power;

/// <summary>
/// Result of <see cref="PowerEstimator.Estimate"/> for one <c>HardwareSnapshot</c>.
/// <paramref name="WallW"/> is the estimated draw at the wall socket (DC load divided
/// by PSU efficiency); it excludes the monitor.
/// </summary>
public sealed record PowerEstimate(
    double CpuW,
    double GpuW,
    double BaselineW,
    double DcTotalW,
    double PsuEfficiency,
    double WallW,
    EstimationQuality Quality,
    string? Notes)
{
    /// <summary>Approximate +/- error fraction to surface in the UI for a given quality.</summary>
    public static double ErrorFraction(EstimationQuality quality) => quality switch
    {
        EstimationQuality.Measured => 0.15,
        EstimationQuality.Mixed => 0.22,
        _ => 0.30,
    };
}
