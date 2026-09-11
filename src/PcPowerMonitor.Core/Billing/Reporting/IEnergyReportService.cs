namespace PcPowerMonitor.Core.Billing.Reporting;

/// <summary>
/// Joins the rollup tables (phase 04) with the tariff (phase 07) into report rows. All
/// money stays in Core so it is unit-testable; the ViewModel only formats. Reads use
/// <c>MonthlyRollup</c> — a whole year is a dozen rows.
/// </summary>
public interface IEnergyReportService
{
    /// <summary>12 rows for <paramref name="year"/> (missing months → 0 kWh) with month-over-month deltas.</summary>
    Task<IReadOnlyList<MonthlyReportRow>> GetMonthlyReportAsync(int year, CancellationToken cancellationToken = default);

    /// <summary>One row per calendar year that has data, oldest first.</summary>
    Task<IReadOnlyList<YearlyReportRow>> GetYearlyReportAsync(CancellationToken cancellationToken = default);

    /// <summary>Distinct years present in <c>MonthlyRollup</c>, newest first. Always includes the current year.</summary>
    Task<IReadOnlyList<int>> GetAvailableYearsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rewrites every <c>DailyRollup</c>/<c>MonthlyRollup</c> cost snapshot to
    /// <paramref name="tariff"/> and returns the number of daily rows changed. Destructive:
    /// the caller must confirm with the user first.
    /// </summary>
    Task<int> RecalculateHistoryAsync(TariffSettings tariff, CancellationToken cancellationToken = default);
}
