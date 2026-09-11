namespace PcPowerMonitor.Core.Storage.Dtos;

/// <summary>Live totals for the current local day, summed straight from raw samples.</summary>
public readonly record struct TodaySummary(
    DateOnly Date,
    double Kwh,
    double AvgW,
    double MaxW,
    double UptimeSeconds);
