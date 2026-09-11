namespace PcPowerMonitor.Core.Ai;

/// <summary>
/// AI text-generation client abstraction. Auth is fully encapsulated per-instance
/// (service-account credentials are baked in at construction, typically via
/// <c>AiClientFactory</c>) — callers never pass secrets through this interface. The
/// real implementation is <see cref="VertexAiClient"/> (Vertex AI, service account);
/// this interface remains a separate seam mainly so tests can substitute a fake.
/// Extends <see cref="IDisposable"/> because the real implementation owns a native
/// RSA/CNG key handle — callers (see <c>AiClientFactory.TryCreate</c> usages) MUST
/// dispose whatever instance they get, typically via <c>using</c>.
/// </summary>
public interface IAiClient : IDisposable
{
    /// <summary>
    /// Sends <paramref name="prompt"/> to the underlying model. Returns the raw
    /// natural-language response text (including any trailing ```json block the model
    /// was asked to produce).
    /// </summary>
    /// <exception cref="AiClientException">Thrown when the underlying API returns a non-success status.</exception>
    Task<string> GenerateAsync(string prompt, CancellationToken ct = default);
}
