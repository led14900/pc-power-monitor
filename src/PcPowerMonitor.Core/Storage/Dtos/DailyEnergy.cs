namespace PcPowerMonitor.Core.Storage.Dtos;

/// <summary>A day's aggregate from <c>DailyRollup</c>, including the snapshotted cost inputs.</summary>
public readonly record struct DailyEnergy(
    DateOnly Date,
    double Kwh,
    double AvgW,
    double MaxW,
    double UptimeSeconds,
    double CostVnd,
    double UnitPriceVnd,
    double VatRate);
