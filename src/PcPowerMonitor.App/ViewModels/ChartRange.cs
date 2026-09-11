namespace PcPowerMonitor.App.ViewModels;

/// <summary>Time span shown on the power chart. Drives the DB query window + bucket.</summary>
public enum ChartRange
{
    /// <summary>Last 60 minutes, raw samples (~1800 points).</summary>
    LastHour,

    /// <summary>Last 24 hours, raw samples in 60s buckets.</summary>
    Last24Hours,

    /// <summary>Last 7 days, served from HourlyRollup (~168 points).</summary>
    Last7Days,
}
