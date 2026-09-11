using PcPowerMonitor.Core.Models;
using PcPowerMonitor.Core.Power;

namespace PcPowerMonitor.Core.Sampling;

/// <summary>
/// One produced sample, broadcast to UI (phase 06) and alert (phase 08) subscribers.
/// Carries the raw <see cref="Snapshot"/>, the derived <see cref="Estimate"/> and the
/// <see cref="EnergyIncrement"/> that this tick folded into the running total.
/// </summary>
public sealed record SampleTick(
    HardwareSnapshot Snapshot,
    PowerEstimate Estimate,
    EnergyIncrement Increment);
