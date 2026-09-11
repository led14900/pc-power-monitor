using PcPowerMonitor.Core.Models;

namespace PcPowerMonitor.Core.Power;

/// <summary>
/// Load-aware "everything except CPU and GPU" baseline: RAM + HDD + Peripheral stay
/// flat (already accurate per user diagnosis); SSD, motherboard and fan wattage
/// interpolate between idle/active using live sensor readings from
/// <see cref="HardwareSnapshot"/>. Falls back to profile-derived flat estimates when a
/// sensor is missing, so behaviour degrades gracefully on machines LHM can't fully read.
/// Pure function: no I/O, no clock.
/// </summary>
public static class LoadScaledBaselineCalculator
{
    private const double Ddr4WattPerStick = 3d;
    private const double Ddr5WattPerStick = 4d;
    private const double HddWatt = 8d;

    // Matches PowerEstimator's own "no sensor" assumption so baseline and CPU/GPU
    // fallbacks stay consistent with each other.
    private const double AssumedLoadFraction = 0.30;

    /// <summary>Load-scaled baseline watts. Input profile is clamped first, so the result is always finite.</summary>
    public static double Calculate(HardwareSnapshot snapshot, HardwareProfile profile)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(profile);
        var p = profile.Clamped();

        var ramWattPerStick = string.Equals(p.RamType, "DDR5", StringComparison.OrdinalIgnoreCase)
            ? Ddr5WattPerStick
            : Ddr4WattPerStick;

        var ramW = p.RamSticks * ramWattPerStick;
        // LHM's Storage hardware type covers HDDs and SSDs alike, and DiskSnapshot
        // carries no drive-type flag, so a non-empty snapshot.Disks list is the whole
        // storage picture and every entry already gets a per-device wattage below
        // (CalculateSsdWatts). Adding the flat HddCount x HddWatt term on top of that
        // would double-count any HDD LHM actually reports. Only fall back to the flat
        // profile-count estimate (SSD *and* HDD) when there is no live disk data at all.
        var hddW = snapshot.Disks.Count == 0 ? p.HddCount * HddWatt : 0d;
        var ssdW = CalculateSsdWatts(snapshot.Disks, p);
        var fanW = CalculateFanWatts(snapshot.Fans, p);
        var mbW = CalculateMainboardWatts(snapshot.Cpu, p);

        return ramW + ssdW + hddW + fanW + mbW + p.PeripheralW;
    }

    // Per-device lerp using LHM "Total Activity" %; devices with no activity reading
    // default to the 50% midpoint. Empty disk list (no LHM storage support) falls back
    // to the profile's SsdCount x idle/active midpoint.
    private static double CalculateSsdWatts(IReadOnlyList<DiskSnapshot> disks, HardwareProfile p)
    {
        if (disks.Count == 0)
            return p.SsdCount * (p.SsdIdleW + p.SsdActiveW) / 2d;

        var total = 0d;
        foreach (var disk in disks)
        {
            var activity = disk.ActivityPercent is { } a && double.IsFinite(a) ? Math.Clamp(a, 0d, 100d) : 50d;
            total += Lerp(p.SsdIdleW, p.SsdActiveW, activity / 100d);
        }
        return total;
    }

    // LHM has no direct motherboard power sensor; CPU load% is the best available
    // proxy for VRM loss (the load-varying part of mainboard draw). Linear
    // interpolation — no established physical model to justify a curve.
    private static double CalculateMainboardWatts(CpuSnapshot? cpu, HardwareProfile p)
    {
        var loadFraction = cpu?.TotalLoadPercent is { } l && double.IsFinite(l)
            ? Math.Clamp(l, 0d, 100d) / 100d
            : AssumedLoadFraction;

        return Lerp(p.MotherboardIdleW, p.MotherboardW, loadFraction);
    }

    // Fan affinity laws: power scales with RPM^3, not linearly with RPM. Fans without
    // an RPM reading (OEM boards often omit this) use the idle/active midpoint; an
    // empty fan list (no fan sensors at all) falls back to FanCount x midpoint.
    private static double CalculateFanWatts(IReadOnlyList<FanSnapshot> fans, HardwareProfile p)
    {
        if (fans.Count == 0)
            return p.FanCount * (p.FanIdleW + p.FanActiveW) / 2d;

        var total = 0d;
        foreach (var fan in fans)
        {
            if (fan.Rpm is { } rpm && double.IsFinite(rpm) && rpm >= 0d)
            {
                var fraction = Math.Clamp(rpm / p.FanMaxRpm, 0d, 1d);
                var cubedFraction = fraction * fraction * fraction; // RPM^3 scaling
                total += Lerp(p.FanIdleW, p.FanActiveW, cubedFraction);
            }
            else
            {
                total += (p.FanIdleW + p.FanActiveW) / 2d;
            }
        }
        return total;
    }

    private static double Lerp(double idle, double active, double t)
        => idle + (active - idle) * Math.Clamp(t, 0d, 1d);
}
