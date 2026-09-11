using PcPowerMonitor.Core.Security;
using PcPowerMonitor.Core.Settings;

namespace PcPowerMonitor.Core.Ai;

/// <summary>
/// Builds the <see cref="VertexAiClient"/> for the current <see cref="AiSettings"/>,
/// decrypting the stored service-account JSON. Returns null (never throws) when the
/// feature isn't configured or the stored secret no longer decrypts — same
/// "not configured" convention as the rest of the AI settings surface (see
/// <c>SettingsValidator.ClampAi</c>), so callers just hide the AI UI instead of having
/// to catch exceptions.
/// </summary>
public static class AiClientFactory
{
    public static IAiClient? TryCreate(AiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrEmpty(settings.EncryptedServiceAccountJson) ||
            !DpapiKeyProtector.TryDecrypt(settings.EncryptedServiceAccountJson, out var json) ||
            string.IsNullOrWhiteSpace(settings.ProjectId) ||
            string.IsNullOrWhiteSpace(settings.ModelId))
            return null;

        try
        {
            return new VertexAiClient(json, settings.ProjectId, settings.Region, settings.ModelId);
        }
        catch (ArgumentException)
        {
            // Decrypted JSON parsed but was missing required fields / malformed PEM —
            // treat exactly like "not configured" rather than crashing the caller.
            return null;
        }
    }
}
