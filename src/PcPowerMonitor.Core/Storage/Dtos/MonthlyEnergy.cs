namespace PcPowerMonitor.Core.Storage.Dtos;

/// <summary>A calendar month's aggregate from <c>MonthlyRollup</c>. <paramref name="Month"/>
/// is <c>'YYYY-MM'</c> local time.</summary>
public readonly record struct MonthlyEnergy(
    string Month,
    double Kwh,
    double CostVnd,
    double UptimeSeconds,
    double MaxW);
