using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PcPowerMonitor.Core.Power;

namespace PcPowerMonitor.Core.Ai;

/// <summary>
/// Extracts the trailing ```json { ... } ``` block from a Vertex AI response and maps it
/// to <see cref="HardwareProfile"/>. Never throws — malformed/missing JSON degrades to
/// <see cref="HardwareProfileSuggestion.ParsedSuccessfully"/> = false with the raw text
/// preserved so the caller can display it as-is.
/// </summary>
public static class ResponseParser
{
    private static readonly Regex JsonBlockPattern = new(
        @"```json\s*(\{[\s\S]*?\})\s*```",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // PromptBuilder's JSON template instructs the model to return psuRating as a
        // string (e.g. "Gold"), matching how PsuRating serializes elsewhere in this app
        // (see JsonSettingsStore.Options). Without this converter, System.Text.Json
        // expects enums as numbers and every real (well-formed) response fails to parse.
        Converters = { new JsonStringEnumConverter() },
    };

    public static HardwareProfileSuggestion Parse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return new HardwareProfileSuggestion(new HardwareProfile(), response ?? string.Empty, false);

        var match = JsonBlockPattern.Match(response);
        if (!match.Success)
            return new HardwareProfileSuggestion(new HardwareProfile(), response, false);

        try
        {
            var json = match.Groups[1].Value;
            var profile = JsonSerializer.Deserialize<HardwareProfile>(json, SerializerOptions)
                          ?? new HardwareProfile();
            return new HardwareProfileSuggestion(profile.Clamped(), response, true);
        }
        catch (JsonException)
        {
            return new HardwareProfileSuggestion(new HardwareProfile(), response, false);
        }
    }
}
