using System.Text.RegularExpressions;

namespace PcPowerMonitor.Core.Hardware;

/// <summary>
/// Strips anything that looks like a serial number from a hardware name before it
/// is written to the log. Disk model names are kept; serials are PII and must not
/// leak into diagnostics.
/// </summary>
public static partial class DiagnosticsScrubber
{
    [GeneratedRegex(@"(?i)\b(s\s*/?\s*n|serial(\s*number)?)\b\s*[:#=]?\s*\S+")]
    private static partial Regex SerialPattern();

    public static string Scrub(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";

        var cleaned = SerialPattern().Replace(name, string.Empty);
        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
        return cleaned.Length == 0 ? "?" : cleaned;
    }
}
