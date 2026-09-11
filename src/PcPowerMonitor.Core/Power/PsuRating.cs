namespace PcPowerMonitor.Core.Power;

/// <summary>
/// 80 PLUS certification tiers. Used to pick a PSU efficiency curve; higher tiers
/// waste less power as heat, so wall power is closer to DC load.
/// </summary>
public enum PsuRating
{
    White,
    Bronze,
    Silver,
    Gold,
    Platinum,
    Titanium,
}
