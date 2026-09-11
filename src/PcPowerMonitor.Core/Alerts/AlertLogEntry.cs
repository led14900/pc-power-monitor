namespace PcPowerMonitor.Core.Alerts;

/// <summary>
/// One line in the in-memory alert log shown on the Settings screen (also written to
/// the file log). <paramref name="Level"/> is a plain string ("Warning" / "Info") to
/// keep the record free of any UI or logging dependency.
/// </summary>
public sealed record AlertLogEntry(
    DateTimeOffset TimestampUtc,
    AlertKind Kind,
    string Message,
    string Level);
