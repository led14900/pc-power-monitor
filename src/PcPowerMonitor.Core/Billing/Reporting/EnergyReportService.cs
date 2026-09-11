using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.Core.Storage;

namespace PcPowerMonitor.Core.Billing.Reporting;

/// <inheritdoc />
public sealed partial class EnergyReportService : IEnergyReportService
{
    private const string MonthlyByYearSql =
        "SELECT month_local, kwh, cost_vnd, uptime_seconds FROM MonthlyRollup " +
        "WHERE month_local >= @from AND month_local <= @to ORDER BY month_local";

    private const string YearlyTotalsSql =
        "SELECT substr(month_local, 1, 4) AS y, SUM(kwh), SUM(cost_vnd), SUM(uptime_seconds) " +
        "FROM MonthlyRollup GROUP BY y ORDER BY y";

    private const string DistinctYearsSql =
        "SELECT DISTINCT substr(month_local, 1, 4) FROM MonthlyRollup ORDER BY 1 DESC";

    private readonly SqliteConnectionFactory _factory;
    private readonly IElectricityCostCalculator _calculator;
    private readonly ITariffProvider _tariffProvider;
    private readonly ILogger<EnergyReportService>? _log;

    public EnergyReportService(
        SqliteConnectionFactory factory,
        IElectricityCostCalculator calculator,
        ITariffProvider tariffProvider,
        ILogger<EnergyReportService>? log = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        _tariffProvider = tariffProvider ?? throw new ArgumentNullException(nameof(tariffProvider));
        _log = log;
    }

    public Task<IReadOnlyList<MonthlyReportRow>> GetMonthlyReportAsync(
        int year, CancellationToken cancellationToken = default)
        => Task.Run<IReadOnlyList<MonthlyReportRow>>(() =>
        {
            using var conn = _factory.CreateReadConnection();
            var rows = conn.Query(
                MonthlyByYearSql,
                r => (Month: r.GetString(0), Kwh: r.GetDouble(1), Cost: r.GetDouble(2), Uptime: r.GetDouble(3)),
                ("@from", Ym(year - 1, 12)),
                ("@to", Ym(year, 12)));

            var byMonth = new Dictionary<int, MonthAggregate>(12);
            MonthAggregate? previousDecember = null;

            foreach (var row in rows)
            {
                if (!TryParseYm(row.Month, out var rowYear, out var rowMonth)) continue;
                var aggregate = new MonthAggregate(row.Kwh, row.Cost, row.Uptime);
                if (rowYear == year) byMonth[rowMonth] = aggregate;
                else if (rowYear == year - 1 && rowMonth == 12) previousDecember = aggregate;
            }

            return ReportRowFactory.BuildMonthlyRows(
                year, byMonth, previousDecember, _calculator, _tariffProvider.Current);
        }, cancellationToken);

    public Task<IReadOnlyList<YearlyReportRow>> GetYearlyReportAsync(
        CancellationToken cancellationToken = default)
        => Task.Run<IReadOnlyList<YearlyReportRow>>(() =>
        {
            var tariff = _tariffProvider.Current;
            using var conn = _factory.CreateReadConnection();
            var rows = conn.Query(
                YearlyTotalsSql,
                r => (Year: r.GetString(0), Kwh: r.GetDouble(1), Cost: r.GetDouble(2), Uptime: r.GetDouble(3)));

            var result = new List<YearlyReportRow>(rows.Count);
            foreach (var row in rows)
            {
                if (!int.TryParse(row.Year, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
                    continue;
                var (subtotal, vat, total) = ReportRowFactory.SplitCost(
                    new MonthAggregate(row.Kwh, row.Cost, row.Uptime), _calculator, tariff);
                result.Add(new YearlyReportRow(y, row.Kwh, subtotal, vat, total, row.Uptime / 3600d));
            }

            return result;
        }, cancellationToken);

    public Task<IReadOnlyList<int>> GetAvailableYearsAsync(CancellationToken cancellationToken = default)
        => Task.Run<IReadOnlyList<int>>(() =>
        {
            using var conn = _factory.CreateReadConnection();
            var years = conn.Query(
                DistinctYearsSql,
                r => int.TryParse(r.GetString(0), NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
                    ? y
                    : 0)
                .Where(y => y > 0)
                .ToHashSet();

            years.Add(DateTimeOffset.Now.Year);
            return years.OrderByDescending(y => y).ToList();
        }, cancellationToken);

    private static string Ym(int year, int month)
        => string.Create(CultureInfo.InvariantCulture, $"{year:D4}-{month:D2}");

    private static bool TryParseYm(string value, out int year, out int month)
    {
        year = month = 0;
        return value is { Length: 7 }
            && int.TryParse(value.AsSpan(0, 4), NumberStyles.Integer, CultureInfo.InvariantCulture, out year)
            && int.TryParse(value.AsSpan(5, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out month);
    }

    private static double ScalarDouble(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var value = cmd.ExecuteScalar();
        return value is null or DBNull ? 0d : Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }
}
