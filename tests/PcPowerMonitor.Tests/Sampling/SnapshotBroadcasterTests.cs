using PcPowerMonitor.Core.Hardware;
using PcPowerMonitor.Core.Models;
using PcPowerMonitor.Core.Power;
using PcPowerMonitor.Core.Sampling;

namespace PcPowerMonitor.Tests.Sampling;

public sealed class SnapshotBroadcasterTests
{
    private static SampleTick Tick(double wallW = 123d) => new(
        HardwareSnapshot.Empty(SensorAvailability.Full, null),
        new PowerEstimate(40d, 0d, 60d, 100d, 0.9d, wallW, EstimationQuality.Estimated, null),
        EnergyIncrement.Zero(2d));

    [Fact]
    public void Publish_delivers_to_every_subscriber()
    {
        var broadcaster = new SnapshotBroadcaster();
        var seenA = 0;
        var seenB = 0;
        broadcaster.Ticked += _ => seenA++;
        broadcaster.Ticked += _ => seenB++;

        broadcaster.Publish(Tick());

        Assert.Equal(1, seenA);
        Assert.Equal(1, seenB);
    }

    [Fact]
    public void Publish_isolates_a_throwing_subscriber()
    {
        var broadcaster = new SnapshotBroadcaster();
        var reached = false;
        broadcaster.Ticked += _ => throw new InvalidOperationException("boom");
        broadcaster.Ticked += _ => reached = true;

        var ex = Record.Exception(() => broadcaster.Publish(Tick()));

        Assert.Null(ex);
        Assert.True(reached);
    }

    [Fact]
    public void Publish_with_no_subscribers_is_a_no_op()
    {
        var broadcaster = new SnapshotBroadcaster();
        Assert.Null(Record.Exception(() => broadcaster.Publish(Tick())));
    }

    [Fact]
    public void Unsubscribed_handler_stops_receiving()
    {
        var broadcaster = new SnapshotBroadcaster();
        var count = 0;
        void Handler(SampleTick _) => count++;

        broadcaster.Ticked += Handler;
        broadcaster.Publish(Tick());
        broadcaster.Ticked -= Handler;
        broadcaster.Publish(Tick());

        Assert.Equal(1, count);
    }

    [Fact]
    public void Publish_rejects_null()
    {
        var broadcaster = new SnapshotBroadcaster();
        Assert.Throws<ArgumentNullException>(() => broadcaster.Publish(null!));
    }
}
