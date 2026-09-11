namespace PcPowerMonitor.Core.Power;

/// <summary>
/// A monotonic stopwatch abstraction for measuring the gap between samples. Kept
/// separate from wall-clock time so <see cref="EnergyAccumulator"/> is immune to
/// NTP corrections / DST and can be driven deterministically in tests.
/// </summary>
public interface IElapsedClock
{
    /// <summary>Seconds elapsed since construction or the last <see cref="Restart"/>.</summary>
    double ElapsedSeconds { get; }

    /// <summary>Reset the elapsed measurement to zero and keep counting.</summary>
    void Restart();
}
