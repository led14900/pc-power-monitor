using Microsoft.Extensions.Logging;

namespace PcPowerMonitor.Core.Sampling;

/// <summary>
/// Default <see cref="ISnapshotBroadcaster"/>. Invokes each subscriber in isolation so
/// one bad handler cannot starve the rest or kill the sampling loop.
/// </summary>
public sealed class SnapshotBroadcaster : ISnapshotBroadcaster
{
    private readonly ILogger<SnapshotBroadcaster>? _log;

    public SnapshotBroadcaster(ILogger<SnapshotBroadcaster>? log = null) => _log = log;

    public event Action<SampleTick>? Ticked;

    public void Publish(SampleTick tick)
    {
        ArgumentNullException.ThrowIfNull(tick);

        var handlers = Ticked;
        if (handlers is null) return;

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<SampleTick>)handler)(tick);
            }
            catch (Exception ex)
            {
                _log?.LogError(ex, "A snapshot subscriber threw; continuing with the rest.");
            }
        }
    }
}
