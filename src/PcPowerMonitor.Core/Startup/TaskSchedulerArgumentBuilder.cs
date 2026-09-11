namespace PcPowerMonitor.Core.Startup;

/// <summary>
/// Pure builders for the <c>schtasks.exe</c> command lines. Isolated so the quoting
/// for install paths containing spaces (e.g. <c>G:\My Drive\...</c>) is unit-tested
/// without ever touching the real Task Scheduler.
/// </summary>
public static class TaskSchedulerArgumentBuilder
{
    /// <summary>Scheduled-task name. Also used by the query/delete builders.</summary>
    public const string TaskName = "PcPowerMonitorAutoStart";

    /// <summary>Command-line switch the task passes so the app starts hidden after a delay.</summary>
    public const string AutoStartSwitch = "--autostart";

    /// <summary>
    /// <c>/Create</c> line for an ONLOGON, highest-privilege, interactive task.
    /// The run target is one quoted token whose inner exe path is quoted again with
    /// <c>\"</c> so a path with spaces survives schtasks' own parsing.
    /// </summary>
    public static string BuildCreateArguments(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
            throw new ArgumentException("Đường dẫn ứng dụng không được để trống.", nameof(exePath));

        var run = $"\\\"{exePath.Trim()}\\\" {AutoStartSwitch}";
        return $"/Create /SC ONLOGON /TN \"{TaskName}\" /RL HIGHEST /IT /F /TR \"{run}\"";
    }

    /// <summary><c>/Delete</c> line (force, no prompt).</summary>
    public static string BuildDeleteArguments()
        => $"/Delete /TN \"{TaskName}\" /F";

    /// <summary><c>/Query</c> line — exit code 0 means the task exists.</summary>
    public static string BuildQueryArguments()
        => $"/Query /TN \"{TaskName}\"";
}
