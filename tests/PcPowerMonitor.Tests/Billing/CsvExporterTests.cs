using System.Text;
using PcPowerMonitor.Core.Billing;
using PcPowerMonitor.Core.Billing.Reporting;
using PcPowerMonitor.Tests.Fixtures;

namespace PcPowerMonitor.Tests.Billing;

public sealed class CsvExporterTests
{
    private static readonly CsvExporter Exporter = new();

    private static MonthlyReportRow Row(
        int month, double kwh, double subtotal, double vat, double total,
        double? deltaPct = null, double? deltaVnd = null, string? label = null)
        => new(2026, month, label ?? $"Tháng {month}", kwh, subtotal, vat, total, 12.5d,
               deltaPct.HasValue ? kwh : null, deltaPct, deltaVnd);

    private static IReadOnlyList<MonthlyReportRow> SampleRows() => new[]
    {
        Row(1, 100d, 346_000d, 34_600d, 380_600d),
        Row(2, 120d, 415_200d, 41_520d, 456_720d, deltaPct: 20d, deltaVnd: 76_120d),
    };

    [Fact]
    public void File_starts_with_utf8_bom()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "bao-cao.csv");

        Exporter.WriteToFile(path, 2026, SampleRows(), new TariffSettings());

        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length >= 3);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
    }

    [Fact]
    public void Uses_semicolon_separator_and_vietnamese_number_format()
    {
        var csv = Exporter.BuildCsv(2026, SampleRows(), new TariffSettings());

        Assert.Contains("Tháng;kWh;Tiền chưa VAT (đ);VAT (đ);Tổng (đ);Giờ chạy", csv);
        // vi-VN groups thousands with '.' → 380.600
        Assert.Contains("380.600", csv);
        Assert.Contains("100,00", csv); // kWh N2, vi-VN decimal comma
    }

    [Fact]
    public void Cell_with_separator_is_quoted()
    {
        var rows = new[] { Row(1, 1d, 1d, 0d, 1d, label: "Tháng;1") };

        var csv = Exporter.BuildCsv(2026, rows, new TariffSettings());

        Assert.Contains("\"Tháng;1\"", csv);
    }

    [Fact]
    public void Formula_like_source_note_is_prefixed_with_apostrophe()
    {
        var tariff = new TariffSettings { SourceNote = "=CMD()" };

        var csv = Exporter.BuildCsv(2026, SampleRows(), tariff);

        Assert.Contains("'=CMD()", csv);
        Assert.DoesNotContain(";=CMD()", csv);
    }

    [Fact]
    public void Comment_line_reports_price_and_vat()
    {
        var csv = Exporter.BuildCsv(2026, SampleRows(), new TariffSettings());

        Assert.StartsWith("# Đơn giá: 3.460 đ/kWh (bậc 6), VAT 10%", csv);
    }

    [Fact]
    public void Year_total_row_is_appended()
    {
        var csv = Exporter.BuildCsv(2026, SampleRows(), new TariffSettings());

        Assert.Contains("Tổng năm 2026;", csv);
    }
}
