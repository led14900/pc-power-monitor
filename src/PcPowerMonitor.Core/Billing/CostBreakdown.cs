namespace PcPowerMonitor.Core.Billing;

/// <summary>
/// Full money breakdown for a kWh figure. Deliberately <em>unrounded</em> — rounding
/// happens only at the display / CSV boundary so year totals never accumulate error.
/// Distinct from <see cref="PcPowerMonitor.Core.Storage.CostBreakdown"/> (the leaner
/// rollup-snapshot struct) — different namespace, no clash.
/// </summary>
public sealed record CostBreakdown(
    double KwhTotal,
    double UnitPrice,
    double SubtotalVnd,
    double VatVnd,
    double TotalVnd)
{
    /// <summary>An all-zero breakdown (no energy).</summary>
    public static readonly CostBreakdown Zero = new(0d, 0d, 0d, 0d, 0d);
}
