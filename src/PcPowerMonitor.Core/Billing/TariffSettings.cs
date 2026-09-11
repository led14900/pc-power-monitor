namespace PcPowerMonitor.Core.Billing;

/// <summary>
/// EVN electricity price configuration. Flat tier-6 (marginal-cost) model: every kWh the
/// PC draws is billed at the tier-6 unit price — no progressive 6-tier ladder (see phase 07
/// key insights). Serialised to JSON by the settings store in phase 08; phase 07 only ever
/// sees the defaults via <c>IOptionsMonitor</c>.
/// </summary>
public sealed record TariffSettings
{
    /// <summary>Tier-6 (&gt; 400 kWh) unit price, VND per kWh, before VAT.</summary>
    public double UnitPriceVnd { get; init; } = 3460;

    /// <summary>VAT fraction, e.g. <c>0.10</c> for 10% (Validation Session 1 raised it from 8%).</summary>
    public double VatRate { get; init; } = 0.10;

    /// <summary>When false, cost is the pre-VAT subtotal only.</summary>
    public bool IncludeVat { get; init; } = true;

    /// <summary>Date the configured price took effect. Drives the "stale tariff" banner.</summary>
    public DateOnly EffectiveFrom { get; init; } = new(2025, 5, 9);

    /// <summary>Human note on where the number came from. User-editable in phase 08.</summary>
    public string SourceNote { get; init; } =
        "Bậc 6 (trên 400 kWh) — QĐ 1279/QĐ-BCT ngày 09/5/2025. Kiểm tra lại với hóa đơn EVN.";
}
