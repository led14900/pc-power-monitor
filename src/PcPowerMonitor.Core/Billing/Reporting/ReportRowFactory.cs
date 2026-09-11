namespace PcPowerMonitor.Core.Billing.Reporting;

/// <summary>A month's raw rollup figures before the tariff split is applied.</summary>
public readonly record struct MonthAggregate(double Kwh, double CostVnd, double UptimeSeconds)
{
    public static readonly MonthAggregate Empty = new(0d, 0d, 0d);
}

/// <summary>
/// Pure assembly of report rows from rollup aggregates + a tariff. No DB, no I/O — this is
/// the piece phase 09 exercises hardest. Cost is split back into subtotal/VAT from the
/// stored total when a snapshot exists, otherwise computed fresh from the current tariff.
/// </summary>
public static class ReportRowFactory
{
    private const double SecondsPerHour = 3600d;
    private const double MaxVatRate = 0.5d;

    /// <param name="byMonth">Aggregates keyed by month number 1–12 (absent ⇒ no data).</param>
    /// <param name="previousDecember">Prior-year December, for January's delta (may be null).</param>
    public static IReadOnlyList<MonthlyReportRow> BuildMonthlyRows(
        int year,
        IReadOnlyDictionary<int, MonthAggregate> byMonth,
        MonthAggregate? previousDecember,
        IElectricityCostCalculator calculator,
        TariffSettings currentTariff)
    {
        ArgumentNullException.ThrowIfNull(byMonth);
        ArgumentNullException.ThrowIfNull(calculator);
        ArgumentNullException.ThrowIfNull(currentTariff);

        var rows = new List<MonthlyReportRow>(12);
        for (var month = 1; month <= 12; month++)
        {
            var current = byMonth.TryGetValue(month, out var agg) ? agg : MonthAggregate.Empty;
            var previous = month == 1
                ? previousDecember
                : byMonth.TryGetValue(month - 1, out var prev) ? prev : null;

            var (subtotal, vat, total) = SplitCost(current, calculator, currentTariff);
            var (deltaKwh, deltaPercent, deltaVnd) = Delta(current, previous, total, calculator, currentTariff);

            rows.Add(new MonthlyReportRow(
                year, month, $"Tháng {month}",
                current.Kwh, subtotal, vat, total,
                current.UptimeSeconds / SecondsPerHour,
                deltaKwh, deltaPercent, deltaVnd));
        }

        return rows;
    }

    /// <summary>Subtotal / VAT / total for one aggregate, preferring the stored snapshot.</summary>
    public static (double Subtotal, double Vat, double Total) SplitCost(
        MonthAggregate aggregate, IElectricityCostCalculator calculator, TariffSettings currentTariff)
    {
        if (aggregate.CostVnd > 0d)
        {
            var vatRate = currentTariff.IncludeVat
                ? Math.Clamp(currentTariff.VatRate, 0d, MaxVatRate)
                : 0d;
            var subtotal = vatRate > 0d ? aggregate.CostVnd / (1d + vatRate) : aggregate.CostVnd;
            return (subtotal, aggregate.CostVnd - subtotal, aggregate.CostVnd);
        }

        if (aggregate.Kwh <= 0d) return (0d, 0d, 0d);

        var fresh = calculator.Calculate(aggregate.Kwh, currentTariff);
        return (fresh.SubtotalVnd, fresh.VatVnd, fresh.TotalVnd);
    }

    private static (double? Kwh, double? Percent, double? Vnd) Delta(
        MonthAggregate current, MonthAggregate? previous, double currentTotal,
        IElectricityCostCalculator calculator, TariffSettings tariff)
    {
        if (previous is not { } prev) return (null, null, null);

        var (_, _, prevTotal) = SplitCost(prev, calculator, tariff);
        var deltaKwh = current.Kwh - prev.Kwh;
        var deltaVnd = currentTotal - prevTotal;
        double? percent = prev.Kwh > 0d ? deltaKwh / prev.Kwh * 100d : null;
        return (deltaKwh, percent, deltaVnd);
    }
}
