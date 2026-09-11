namespace PcPowerMonitor.Core.Power;

/// <summary>
/// Maps (80 PLUS rating, PSU load fraction) to an efficiency 0..1 by linear
/// interpolation over the published 20% / 50% / 100% marks. Below 20% efficiency
/// drops sharply, so a separate <c>at10</c> mark (= at20 - 0.07) is used and the
/// curve is not extrapolated linearly toward zero.
/// </summary>
public static class PsuEfficiencyTable
{
    /// <summary>Lower / upper bounds on the returned efficiency, to stop absurd wall-power math.</summary>
    public const double MinEfficiency = 0.60;
    public const double MaxEfficiency = 0.96;

    private const double VeryLowLoadDrop = 0.07;

    /// <summary>
    /// Efficiency for the given load fraction (DC load / PSU rated watts).
    /// loadFraction &lt;= 0.10 (or non-finite) → the very-low-load mark;
    /// loadFraction &gt;= 1.0 → the 100% mark (overload clamps here).
    /// </summary>
    public static double Lookup(PsuRating rating, double loadFraction)
    {
        var (at20, at50, at100) = Marks(rating);
        var at10 = at20 - VeryLowLoadDrop;

        double efficiency;
        if (!double.IsFinite(loadFraction) || loadFraction <= 0.10)
            efficiency = at10;
        else if (loadFraction >= 1.0)
            efficiency = at100;
        else if (loadFraction < 0.20)
            efficiency = Interpolate(0.10, at10, 0.20, at20, loadFraction);
        else if (loadFraction < 0.50)
            efficiency = Interpolate(0.20, at20, 0.50, at50, loadFraction);
        else
            efficiency = Interpolate(0.50, at50, 1.00, at100, loadFraction);

        return Math.Clamp(efficiency, MinEfficiency, MaxEfficiency);
    }

    private static double Interpolate(double x0, double y0, double x1, double y1, double x)
        => y0 + (y1 - y0) * ((x - x0) / (x1 - x0));

    // Published 80 PLUS efficiency at 20% / 50% / 100% load (115V internal).
    private static (double At20, double At50, double At100) Marks(PsuRating rating) => rating switch
    {
        PsuRating.White => (0.78, 0.80, 0.77),
        PsuRating.Bronze => (0.82, 0.85, 0.82),
        PsuRating.Silver => (0.85, 0.88, 0.85),
        PsuRating.Gold => (0.87, 0.90, 0.87),
        PsuRating.Platinum => (0.90, 0.92, 0.89),
        PsuRating.Titanium => (0.92, 0.94, 0.90),
        _ => (0.82, 0.85, 0.82),
    };
}
