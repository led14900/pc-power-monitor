using System.Windows;
using System.Windows.Threading;
using H.NotifyIcon;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.Core.Alerts;

namespace PcPowerMonitor.App.Tray;

/// <summary>
/// <see cref="ITrayNotifier"/> over the H.NotifyIcon <c>TaskbarIcon</c> owned by
/// <see cref="TrayIconHost"/>. Marshals onto the UI dispatcher and swallows any
/// failure (Windows notifications turned off is a normal, non-fatal state — the
/// on-screen alert log still records the event).
/// </summary>
/// <remarks>
/// <see cref="TrayIconHost"/> is taken as a <see cref="Lazy{T}"/>: it drags in the whole
/// window / view-model graph, and both <c>AlertService</c> and <c>SettingsViewModel</c>
/// depend on this notifier — resolving the host eagerly closes a DI cycle
/// (AlertService → ITrayNotifier → TrayIconHost → … → MainViewModel → SettingsViewModel
/// → ITrayNotifier). The icon is only needed when an alert actually fires, long after
/// <c>App.OnStartup</c> has built the host, so deferring the resolution is free.
/// </remarks>
public sealed class TrayNotifier : ITrayNotifier
{
    private readonly Lazy<TrayIconHost> _host;
    private readonly ILogger<TrayNotifier>? _log;
    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;

    public TrayNotifier(Lazy<TrayIconHost> host, ILogger<TrayNotifier>? log = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _log = log;
    }

    public void Notify(string title, string message, string level)
    {
        _dispatcher.BeginInvoke(() =>
        {
            try
            {
                _host.Value.Icon?.ShowNotification(title: title, message: message);
            }
            catch (Exception ex)
            {
                _log?.LogDebug(ex, "Thông báo Windows bị tắt hoặc lỗi khi hiển thị.");
            }
        });
    }
}
