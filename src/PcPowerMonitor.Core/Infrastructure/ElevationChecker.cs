using System.Security.Principal;

namespace PcPowerMonitor.Core.Infrastructure;

/// <summary>
/// Reports whether the current process runs with Administrator rights.
/// The sensor driver only loads when elevated; the UI uses this to show a warning banner.
/// </summary>
public static class ElevationChecker
{
    public static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            // If we cannot even query the token, assume not elevated (safe default).
            return false;
        }
    }
}
