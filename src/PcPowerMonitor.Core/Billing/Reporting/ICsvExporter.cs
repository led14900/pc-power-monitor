namespace PcPowerMonitor.Core.Billing.Reporting;

/// <summary>
/// Writes a monthly report as Excel-friendly Vietnamese CSV (UTF-8 BOM, <c>;</c> separator,
/// vi-VN numbers). Stream-based so it is unit-testable without touching the disk.
/// </summary>
public interface ICsvExporter
{
    /// <summary>Render to a string (no BOM — that is an encoding concern of the caller).</summary>
    string BuildCsv(int year, IReadOnlyList<MonthlyReportRow> rows, TariffSettings tariff);

    /// <summary>
    /// Write the report to <paramref name="path"/> as UTF-8 <em>with</em> BOM.
    /// Lets <see cref="IOException"/> / <see cref="UnauthorizedAccessException"/> bubble
    /// so the caller can show a friendly "file open in Excel" message.
    /// </summary>
    void WriteToFile(string path, int year, IReadOnlyList<MonthlyReportRow> rows, TariffSettings tariff);
}
