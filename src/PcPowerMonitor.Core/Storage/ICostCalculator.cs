namespace PcPowerMonitor.Core.Storage;

/// <summary>
/// Turns a day's energy into money for <c>DailyRollup.cost_vnd</c>. The rollup stores a
/// <em>snapshot</em> of the price used and only ever costs a day once (see
/// <c>RollupSqlStatements.DailyCostSelect</c>), so a later EVN tariff change never
/// rewrites history — repricing the past is the explicit job of
/// <c>EnergyReportService.RecalculateHistoryAsync</c>. Phase 07 swaps in the real
/// EVN-tiered implementation via DI without touching <see cref="RollupService"/>.
/// </summary>
public interface ICostCalculator
{
    CostBreakdown Calculate(double kwh, DateOnly localDate);
}

/// <summary>Money plus the inputs used to derive it, for auditability.</summary>
public readonly record struct CostBreakdown(double CostVnd, double UnitPriceVnd, double VatRate);
