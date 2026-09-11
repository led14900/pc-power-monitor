namespace PcPowerMonitor.Core.Sampling;

/// <summary>
/// In-process pub/sub for <see cref="SampleTick"/>s. Deliberately has no WPF
/// dependency — subscribers (the dashboard) marshal onto the UI Dispatcher
/// themselves. A throwing subscriber must not break delivery to the others.
/// </summary>
public interface ISnapshotBroadcaster
{
    /// <summary>Raised once per produced sample, on the sampling-loop thread.</summary>
    event Action<SampleTick>? Ticked;

    /// <summary>Fan a tick out to every current subscriber.</summary>
    void Publish(SampleTick tick);
}
