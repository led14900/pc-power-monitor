namespace PcPowerMonitor.Core.Settings;

/// <summary>
/// Whether the AI prompt should reason about the machine as discrete swappable parts
/// (desktop — CPU/GPU/PSU chosen independently) or as one fixed factory unit (laptop —
/// the exact model already implies battery/charger/whole-chassis wattage, which is far
/// more reliably looked up as a single spec sheet than reconstructed component-by-component).
/// </summary>
public enum AiDeviceType
{
    Desktop,
    Laptop,
}

/// <summary>
/// Configuration for the optional "AI-assisted hardware profile lookup" feature,
/// backed by Google Cloud Vertex AI. Entirely opt-in: a null
/// <see cref="EncryptedServiceAccountJson"/> means the feature is not configured and its
/// UI stays hidden. The service-account key itself is never stored in plain text — see
/// <c>PcPowerMonitor.Core.Security.DpapiKeyProtector</c>.
/// </summary>
public sealed record AiSettings
{
    /// <summary>
    /// Vertex AI model id (e.g. "gemini-3.5-flash-lite"). User-editable since Google
    /// renames/versions models over time and hardcoding one would go stale.
    /// </summary>
    public string ModelId { get; init; } = "gemini-3.5-flash-lite";

    /// <summary>
    /// Optional user-typed machine model (e.g. "Lenovo ThinkCentre M700 Tiny" or
    /// "Dell XPS 15 9530"). Greatly improves AI accuracy for motherboard/PSU (desktop)
    /// or battery/charger (laptop) wattage guesses, which can't be inferred from
    /// CPU/RAM/disk sensor names alone.
    /// </summary>
    public string? MachineModel { get; init; }

    /// <summary>Steers the lookup prompt: per-component (desktop) vs whole-unit (laptop). See <see cref="AiDeviceType"/>.</summary>
    public AiDeviceType DeviceType { get; init; } = AiDeviceType.Desktop;

    /// <summary>
    /// Base64 DPAPI blob of the full Vertex AI service-account JSON key file (client_email +
    /// private_key + project_id, etc). Null/empty = Vertex not configured. See
    /// <c>PcPowerMonitor.Core.Security.DpapiKeyProtector</c>.
    /// </summary>
    public string? EncryptedServiceAccountJson { get; init; }

    /// <summary>Google Cloud project id the service account belongs to. Required for the feature to work.</summary>
    public string? ProjectId { get; init; }

    /// <summary>Vertex AI region hosting the model (e.g. "us-central1").</summary>
    public string Region { get; init; } = "us-central1";
}
