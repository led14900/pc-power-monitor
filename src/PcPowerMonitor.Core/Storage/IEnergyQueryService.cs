using PcPowerMonitor.Core.Storage.Dtos;

namespace PcPowerMonitor.Core.Storage;

/// <summary>
/// Read-side API for the dashboard and reports. Uses its own connections (WAL allows
/// concurrent reads while the writer holds its transaction).
/// </summary>
public interface IEnergyQueryService
{
    /// <summary>
    /// Power over time in <paramref name="bucketSeconds"/> buckets. Ranges up to 24h read
    /// raw samples; longer ranges read HourlyRollup to avoid pulling millions of rows.
    /// </summary>
    IReadOnlyList<PowerPoint> GetPowerSeries(DateTimeOffset from, DateTimeOffset to, int bucketSeconds);

    /// <summary>Daily aggregates for <c>[from, to]</c> (inclusive, local dates).</summary>
    IReadOnlyList<DailyEnergy> GetDailyRange(DateOnly from, DateOnly to);

    /// <summary>Every month of the given calendar year that has data.</summary>
    IReadOnlyList<MonthlyEnergy> GetMonthly(int year);

    /// <summary>Running totals for the current local day.</summary>
    TodaySummary GetTodaySummary();

    /// <summary>Lifetime kWh from <c>Meta</c> — seeds the energy accumulator on restart.</summary>
    double GetLastTotalKwh();
}
