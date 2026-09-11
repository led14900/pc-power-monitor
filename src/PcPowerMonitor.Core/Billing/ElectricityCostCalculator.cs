namespace PcPowerMonitor.Core.Billing;

/// <summary>
/// EVN flat tier-6 cost. Formula, and the only one: <c>total = kwh × price × (1 + vat)</c>.
/// Guards clamp bad input rather than throwing (the caller is a background rollup loop).
/// See <c>ElectricityCostCalculator.Adapter.cs</c> for the
/// <see cref="PcPowerMonitor.Core.Storage.ICostCalculator"/> half.
/// </summary>
public sealed partial class ElectricityCostCalculator : IElectricityCostCalculator
{
    /// <summary>Tier-6 price fallback when the configured value is missing / non-positive.</summary>
    private const double FallbackUnitPriceVnd = 3460d;

    /// <summary>VAT can never sensibly exceed 50%; a bad JSON value is clamped, not trusted.</summary>
    private const double MaxVatRate = 0.5d;

    public CostBreakdown Calculate(double kwh, TariffSettings tariff)
    {
        ArgumentNullException.ThrowIfNull(tariff);

        if (!double.IsFinite(kwh) || kwh < 0d) kwh = 0d;

        var price = tariff.UnitPriceVnd is > 0d and <= 100_000d
            ? tariff.UnitPriceVnd
            : FallbackUnitPriceVnd;

        var vat = tariff.IncludeVat
            ? Math.Clamp(double.IsFinite(tariff.VatRate) ? tariff.VatRate : 0d, 0d, MaxVatRate)
            : 0d;

        var subtotal = kwh * price;
        var vatAmount = subtotal * vat;
        return new CostBreakdown(kwh, price, subtotal, vatAmount, subtotal + vatAmount);
    }
}
