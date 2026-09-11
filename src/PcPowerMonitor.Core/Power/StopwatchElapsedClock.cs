using System.Diagnostics;

namespace PcPowerMonitor.Core.Power;

/// <summary>
/// Production <see cref="IElapsedClock"/> backed by <see cref="Stopwatch"/> (QPC).
/// Note: QPC keeps running while the machine sleeps, so a resume shows up as a huge
/// <see cref="ElapsedSeconds"/> — that is exactly the signal the accumulator's gap
/// detection relies on.
/// </summary>
public sealed class StopwatchElapsedClock : IElapsedClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public double ElapsedSeconds => _stopwatch.Elapsed.TotalSeconds;

    public void Restart() => _stopwatch.Restart();
}
