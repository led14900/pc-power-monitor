using PcPowerMonitor.Core.Ai;

namespace PcPowerMonitor.Tests.Ai;

/// <summary>
/// Test double for <see cref="IAiClient"/>. Never makes a network call — records the
/// prompt it received and returns/throws whatever the test configured. Used by AI
/// subsystem integration tests to keep them deterministic and offline.
/// </summary>
public sealed class FakeAiClient : IAiClient
{
    /// <summary>The prompt passed to the most recent <see cref="GenerateAsync"/> call, if any.</summary>
    public string? PromptReceived { get; private set; }

    /// <summary>Number of times <see cref="GenerateAsync"/> was invoked.</summary>
    public int CallCount { get; private set; }

    public string ResponseToReturn { get; set; } = string.Empty;

    public AiClientException? ExceptionToThrow { get; set; }

    /// <summary>Set to true if <see cref="Dispose"/> was called — lets a test assert the caller cleaned up.</summary>
    public bool Disposed { get; private set; }

    public Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        CallCount++;
        PromptReceived = prompt;

        if (ExceptionToThrow is not null)
            throw ExceptionToThrow;

        return Task.FromResult(ResponseToReturn);
    }

    public void Dispose() => Disposed = true;
}
