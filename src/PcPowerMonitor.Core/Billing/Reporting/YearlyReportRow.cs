namespace PcPowerMonitor.Core.Billing.Reporting;

/// <summary>One row of the by-year report: a calendar year's totals from <c>MonthlyRollup</c>.</summary>
public sealed record YearlyReportRow(
    int Year,
    double Kwh,
    double SubtotalVnd,
    double VatVnd,
    double TotalVnd,
    double UptimeHours);
