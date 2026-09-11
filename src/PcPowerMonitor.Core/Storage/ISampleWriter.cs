namespace PcPowerMonitor.Core.Storage;

/// <summary>
/// Non-blocking sink for sensor samples. Implementations buffer and flush in batches.
/// Losing a handful of buffered samples on a hard crash is acceptable; blocking the
/// sampling loop is not.
/// </summary>
public interface ISampleWriter : IAsyncDisposable
{
    /// <summary>Queue a sample. Never blocks, never throws.</summary>
    void Enqueue(SampleRecord record);

    /// <summary>Force everything currently queued to disk.</summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
