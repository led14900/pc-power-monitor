using System.Globalization;
using System.Linq;
using System.Text;

namespace PcPowerMonitor.Core.Billing.Reporting;

/// <inheritdoc />
public sealed class CsvExporter : ICsvExporter
{
    private const char Separator = ';';
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly UTF8Encoding Utf8Bom = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly char[] DangerousLeading = { '=', '+', '-', '@' };

    private static readonly string[] Header =
    {
        "Tháng", "kWh", "Tiền chưa VAT (đ)", "VAT (đ)", "Tổng (đ)", "Giờ chạy",
        "So tháng trước (%)", "So tháng trước (đ)",
    };

    public string BuildCsv(int year, IReadOnlyList<MonthlyReportRow> rows, TariffSettings tariff)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(tariff);

        var sb = new StringBuilder();
        sb.Append(CommentLine(tariff)).Append("\r\n");
        sb.Append(string.Join(Separator, Header.Select(EscapeCell))).Append("\r\n");

        double totalKwh = 0d, totalSub = 0d, totalVat = 0d, totalAll = 0d, totalHours = 0d;
        foreach (var row in rows)
        {
            totalKwh += row.Kwh;
            totalSub += row.SubtotalVnd;
            totalVat += row.VatVnd;
            totalAll += row.TotalVnd;
            totalHours += row.UptimeHours;
            sb.Append(DataLine(row)).Append("\r\n");
        }

        sb.Append(string.Join(Separator, new[]
        {
            EscapeCell($"Tổng năm {year}"),
            Num(totalKwh, "N2"), Num(totalSub, "N0"), Num(totalVat, "N0"),
            Num(totalAll, "N0"), Num(totalHours, "N1"), string.Empty, string.Empty,
        })).Append("\r\n");

        return sb.ToString();
    }

    public void WriteToFile(string path, int year, IReadOnlyList<MonthlyReportRow> rows, TariffSettings tariff)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllText(path, BuildCsv(year, rows, tariff), Utf8Bom);
    }

    private static string CommentLine(TariffSettings tariff)
    {
        var vatPercent = Math.Round((tariff.IncludeVat ? tariff.VatRate : 0d) * 100d, MidpointRounding.AwayFromZero);
        return string.Format(
            Vi,
            "# Đơn giá: {0:N0} đ/kWh (bậc 6), VAT {1:0.##}% — số liệu ƯỚC TÍNH. {2}",
            tariff.UnitPriceVnd, vatPercent, Sanitize(tariff.SourceNote));
    }

    private static string DataLine(MonthlyReportRow row) => string.Join(Separator, new[]
    {
        EscapeCell(row.MonthLabel),
        Num(row.Kwh, "N2"),
        Num(row.SubtotalVnd, "N0"),
        Num(row.VatVnd, "N0"),
        Num(row.TotalVnd, "N0"),
        Num(row.UptimeHours, "N1"),
        row.DeltaPercent is { } p ? Num(p, "N1") : "—",
        row.DeltaVnd is { } d ? Num(d, "N0") : "—",
    });

    private static string Num(double value, string format)
    {
        var digits = format == "N2" ? 2 : format == "N1" ? 1 : 0;
        var rounded = Math.Round(value, digits, MidpointRounding.AwayFromZero);
        return EscapeCell(rounded.ToString(format, Vi));
    }

    /// <summary>Neutralise a value Excel could read as a formula.</summary>
    private static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length > 0 && Array.IndexOf(DangerousLeading, value[0]) >= 0
            ? "'" + value
            : value;
    }

    private static string EscapeCell(string? value)
    {
        var cell = Sanitize(value);
        if (cell.IndexOf(Separator) < 0 && cell.IndexOf('"') < 0 &&
            cell.IndexOf('\n') < 0 && cell.IndexOf('\r') < 0)
            return cell;
        return "\"" + cell.Replace("\"", "\"\"") + "\"";
    }
}
