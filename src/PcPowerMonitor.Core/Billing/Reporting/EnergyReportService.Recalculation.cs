using Microsoft.Extensions.Logging;
using PcPowerMonitor.Core.Storage;

namespace PcPowerMonitor.Core.Billing.Reporting;

/// <summary>
/// The destructive half of <see cref="EnergyReportService"/>: overwrite every stored cost
/// snapshot with a new tariff. Runs in one write transaction; monthly totals are rebuilt
/// from the freshly costed daily rows using the phase-04 rollup statement.
/// </summary>
public sealed partial class EnergyReportService
{
    private const string TotalDailyCostSql = "SELECT COALESCE(SUM(cost_vnd), 0) FROM DailyRollup";
    private const string AllDailyKwhSql = "SELECT date_local, kwh FROM DailyRollup";

    public Task<int> RecalculateHistoryAsync(
        TariffSettings tariff, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tariff);
        return Task.Run(() => Recalculate(tariff), cancellationToken);
    }

    private int Recalculate(TariffSettings tariff)
    {
        List<(string Date, double Kwh)> days;
        double oldTotal;
        using (var read = _factory.CreateReadConnection())
        {
            oldTotal = ScalarDouble(read, TotalDailyCostSql);
            days = read.Query(AllDailyKwhSql, r => (r.GetString(0), r.GetDouble(1)));
        }

        using (var conn = _factory.CreateWriteConnection())
        using (var tx = conn.BeginTransaction())
        {
            foreach (var (date, kwh) in days)
            {
                var b = _calculator.Calculate(kwh, tariff);
                var effectiveVat = b.SubtotalVnd > 0d ? b.VatVnd / b.SubtotalVnd : 0d;
                conn.ExecNonQuery(tx, RollupSqlStatements.DailyCostUpdate,
                    ("@c", b.TotalVnd), ("@p", b.UnitPrice), ("@v", effectiveVat), ("@d", date));
            }

            conn.ExecNonQuery(tx, RollupSqlStatements.Monthly);
            tx.Commit();
        }

        double newTotal;
        using (var read = _factory.CreateReadConnection())
            newTotal = ScalarDouble(read, TotalDailyCostSql);

        _log?.LogInformation(
            "Đã tính lại {Count} ngày theo đơn giá {Price:F0} đ/kWh, VAT {Vat:P0}. " +
            "Tổng tiền: {Old:F0} → {New:F0} đ.",
            days.Count, tariff.UnitPriceVnd, tariff.IncludeVat ? tariff.VatRate : 0d, oldTotal, newTotal);

        return days.Count;
    }
}
