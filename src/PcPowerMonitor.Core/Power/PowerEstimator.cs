using Microsoft.Extensions.Logging;
using PcPowerMonitor.Core.Models;

namespace PcPowerMonitor.Core.Power;

/// <summary>
/// Turns a <see cref="HardwareSnapshot"/> plus a <see cref="HardwareProfile"/> into an
/// estimated wall-socket power draw. Pure function: no I/O, no clock. Fallback to
/// <c>TDP x load%</c> is per-component, so a measured CPU with an unmeasured GPU
/// still yields a partly-real number.
/// </summary>
public sealed class PowerEstimator
{
    private const double DefaultPsuWattage = 650d;
    private const double AssumedLoadFraction = 0.30;

    private readonly ILogger<PowerEstimator>? _log;

    public PowerEstimator(ILogger<PowerEstimator>? logger = null) => _log = logger;

    public PowerEstimate Estimate(HardwareSnapshot snapshot, HardwareProfile profile)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(profile);

        var p = profile.Clamped();
        var notes = new List<string>();

        var cpuW = ResolveCpuW(snapshot.Cpu, p, notes, out var cpuMeasured);
        var gpuW = ResolveGpuW(snapshot.Gpus, p, notes, out var gpuApplicable, out var gpuMeasured);
        var baselineW = LoadScaledBaselineCalculator.Calculate(snapshot, p);
        var dcTotal = cpuW + gpuW + baselineW;

        var psuWattage = (double)p.PsuWattage;
        if (profile.PsuWattage <= 0)
        {
            psuWattage = DefaultPsuWattage;
            notes.Add($"PsuWattage <= 0; defaulted to {DefaultPsuWattage:0}W.");
            _log?.LogWarning("HardwareProfile.PsuWattage was {Value}; using {Default}W.", profile.PsuWattage, DefaultPsuWattage);
        }

        // PSU load fraction is computed from DC total power (the load BEFORE the
        // efficiency division), never from wall power. Using wall power here would
        // make efficiency depend on itself (eta -> load -> eta ...). Break the loop.
        var loadFraction = dcTotal / psuWattage;
        var efficiency = PsuEfficiencyTable.Lookup(p.PsuRating, loadFraction);
        var wallW = dcTotal / efficiency;

        var quality = ResolveQuality(cpuMeasured, gpuApplicable, gpuMeasured);

        return new PowerEstimate(
            cpuW, gpuW, baselineW, dcTotal, efficiency, wallW, quality,
            notes.Count == 0 ? null : string.Join(" ", notes));
    }

    // Measured if every applicable component was sensor-read; Estimated if none were;
    // Mixed otherwise. A machine with no discrete GPU has only the CPU as applicable.
    private static EstimationQuality ResolveQuality(bool cpuMeasured, bool gpuApplicable, bool gpuMeasured)
    {
        var anyMeasured = cpuMeasured || (gpuApplicable && gpuMeasured);
        var anyEstimated = !cpuMeasured || (gpuApplicable && !gpuMeasured);

        if (anyMeasured && anyEstimated) return EstimationQuality.Mixed;
        return anyMeasured ? EstimationQuality.Measured : EstimationQuality.Estimated;
    }

    private static double ResolveCpuW(
        CpuSnapshot? cpu, HardwareProfile profile, List<string> notes, out bool measured)
    {
        if (cpu?.PackagePowerW is { } power && double.IsFinite(power) && power >= 0d)
        {
            measured = true;
            return power;
        }

        measured = false;

        if (cpu?.TotalLoadPercent is { } load && double.IsFinite(load))
        {
            notes.Add("CPU power sensor missing; estimated from TDP x load%.");
            return profile.CpuTdpW * Math.Clamp(load, 0d, 100d) / 100d;
        }

        notes.Add($"CPU power + load missing; assumed TDP x {AssumedLoadFraction:P0}.");
        return profile.CpuTdpW * AssumedLoadFraction;
    }

    private static double ResolveGpuW(
        IReadOnlyList<GpuSnapshot>? gpus, HardwareProfile profile, List<string> notes,
        out bool applicable, out bool measured)
    {
        var total = 0d;
        var sensorSeen = false;
        var fallbackUsed = false;

        if (gpus is { Count: > 0 })
        {
            foreach (var gpu in gpus)
            {
                if (gpu.PowerW is { } power && double.IsFinite(power) && power >= 0d)
                {
                    total += power;
                    sensorSeen = true;
                }
                else if (profile.GpuTdpW > 0d)
                {
                    var load = gpu.CoreLoadPercent is { } l && double.IsFinite(l)
                        ? Math.Clamp(l, 0d, 100d)
                        : AssumedLoadFraction * 100d;
                    total += profile.GpuTdpW * load / 100d;
                    fallbackUsed = true;
                }
            }
        }
        else if (profile.GpuTdpW > 0d)
        {
            total += profile.GpuTdpW * AssumedLoadFraction;
            fallbackUsed = true;
        }

        if (fallbackUsed)
            notes.Add("GPU power sensor missing; estimated from TDP x load%.");

        // Not applicable = no discrete GPU and nothing to estimate → GPU term is exactly 0.
        applicable = sensorSeen || fallbackUsed;
        measured = sensorSeen && !fallbackUsed;
        return total;
    }
}
