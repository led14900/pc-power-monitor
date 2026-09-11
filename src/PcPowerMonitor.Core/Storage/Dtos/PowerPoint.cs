namespace PcPowerMonitor.Core.Storage.Dtos;

/// <summary>One point on a power-over-time chart. <paramref name="Timestamp"/> is the
/// bucket start. From raw samples for short ranges, from HourlyRollup for long ones.</summary>
public readonly record struct PowerPoint(DateTimeOffset Timestamp, double AvgW, double MaxW, double Kwh);
