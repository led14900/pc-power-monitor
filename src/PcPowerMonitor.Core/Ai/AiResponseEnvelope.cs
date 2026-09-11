using System.Text.Json;

namespace PcPowerMonitor.Core.Ai;

/// <summary>
/// Response-envelope navigation for the Vertex AI <c>generateContent</c> JSON shape:
/// <c>candidates[0].content.parts[0].text</c>. Used by <see cref="VertexAiClient"/>.
/// </summary>
internal static class AiResponseEnvelope
{
    /// <summary>
    /// Navigates candidates[0].content.parts[0].text. Returns empty string (never
    /// throws) if the response shape is unexpected — the caller/parser treats an
    /// empty/malformed response the same as "no JSON block found".
    /// </summary>
    public static string ExtractText(JsonElement json)
    {
        if (!json.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
            return string.Empty;

        var first = candidates[0];
        if (!first.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array ||
            parts.GetArrayLength() == 0)
            return string.Empty;

        return parts[0].TryGetProperty("text", out var text)
            ? text.GetString() ?? string.Empty
            : string.Empty;
    }
}
