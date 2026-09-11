using PcPowerMonitor.Core.Storage;

namespace PcPowerMonitor.Core.Billing;

/// <summary>
/// The <see cref="ICostCalculator"/> half of <see cref="ElectricityCostCalculator"/>:
/// the rollup pipeline (phase 04 <c>RollupService</c>) calls <see cref="Calculate(double, DateOnly)"/>
/// per day; we resolve the tariff in force from <see cref="ITariffProvider"/>, run the pure
/// maths, and map to the leaner snapshot struct stored on <c>DailyRollup</c>. In DI this is
/// the real EVN cost that supersedes the phase-04 zero-cost placeholder.
/// </summary>
public sealed partial class ElectricityCostCalculator : ICostCalculator
{
    private readonly ITariffProvider _tariffProvider;

    public ElectricityCostCalculator(ITariffProvider tariffProvider)
        => _tariffProvider = tariffProvider ?? throw new ArgumentNullException(nameof(tariffProvider));

    /// <summary>
    /// Rollup entry point. <paramref name="localDate"/> is unused by the flat tier-6 model
    /// (v1 keeps a single current price); it stays in the signature so a future
    /// date-banded tariff needs no pipeline change.
    /// </summary>
    Storage.CostBreakdown ICostCalculator.Calculate(double kwh, DateOnly localDate)
    {
        var tariff = _tariffProvider.Current;
        var b = Calculate(kwh, tariff);
        var effectiveVat = b.SubtotalVnd > 0d ? b.VatVnd / b.SubtotalVnd : 0d;
        return new Storage.CostBreakdown(b.TotalVnd, b.UnitPrice, effectiveVat);
    }
}
