namespace PcPowerMonitor.Core.Billing.Reporting;

/// <summary>
/// One row of the month-by-month report. Money is unrounded (REAL from the rollup); the UI
/// and CSV round at render time. The <c>Delta*</c> fields compare against the previous
/// calendar month (January compares against December of the prior year) and are
/// <c>null</c> when there is no comparable previous month or the previous month had zero kWh.
/// </summary>
public sealed record MonthlyReportRow(
    int Year,
    int Month,
    string MonthLabel,
    double Kwh,
    double SubtotalVnd,
    double VatVnd,
    double TotalVnd,
    double UptimeHours,
    double? DeltaKwh,
    double? DeltaPercent,
    double? DeltaVnd)
{
    /// <summary>True when the month has no recorded energy at all.</summary>
    public bool IsEmpty => Kwh <= 0d;
}
