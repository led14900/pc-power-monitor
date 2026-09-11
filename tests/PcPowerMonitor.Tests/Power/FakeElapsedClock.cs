using PcPowerMonitor.Core.Power;

namespace PcPowerMonitor.Tests.Power;

/// <summary>
/// Test double for <see cref="IElapsedClock"/>. The test sets <see cref="ElapsedSeconds"/>
/// to whatever dt it wants the next <c>Add</c> call to observe. This drives energy /
/// gap logic deterministically — it is NOT a mock of hardware data.
/// </summary>
public sealed class FakeElapsedClock : IElapsedClock
{
    public double ElapsedSeconds { get; set; }

    public int RestartCount { get; private set; }

    public void Restart() => RestartCount++;
}
