using PcPowerMonitor.Core.Power;

namespace PcPowerMonitor.Core.Ai;

/// <summary>
/// Result of parsing a Vertex AI response into a <see cref="HardwareProfile"/>. Always
/// carries the raw response text so the UI can show it to the user even when parsing
/// failed (<see cref="ParsedSuccessfully"/> = false), letting them copy values manually.
/// </summary>
public sealed record HardwareProfileSuggestion(
    HardwareProfile Profile,
    string RawResponse,
    bool ParsedSuccessfully);
