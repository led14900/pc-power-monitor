namespace PcPowerMonitor.Core.Alerts;

/// <summary>
/// Shows a transient Windows notification. Implemented in the App layer over
/// H.NotifyIcon; must never throw (notifications disabled in Windows is normal).
/// </summary>
public interface ITrayNotifier
{
    void Notify(string title, string message, string level);
}
