using System.ComponentModel;
using System.Windows;
using PcPowerMonitor.App.Views;

namespace PcPowerMonitor.App.Tray;

/// <summary>
/// Makes <see cref="MainWindow"/> behave like a tray app: minimize or close only
/// hides it (the sampling loop keeps running); only <see cref="RequestExit"/> ends the
/// process. The first close-to-tray fires <see cref="FirstHideNotifier"/> so the tray
/// can show a "still running" balloon.
/// </summary>
public sealed class WindowVisibilityService
{
    private readonly Window _window;
    private bool _firstHideDone;
    private bool _exiting;

    public WindowVisibilityService(MainWindow window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _window.StateChanged += OnStateChanged;
        _window.Closing += OnClosing;
    }

    /// <summary>Set by <c>TrayIconHost</c>; invoked once, on the first close-to-tray.</summary>
    public Action? FirstHideNotifier { get; set; }

    /// <summary>Invoked when the window goes to tray — dashboard stops updating.</summary>
    public Action? Suspended { get; set; }

    /// <summary>Invoked when the window is shown again — dashboard resumes.</summary>
    public Action? Resumed { get; set; }

    public void ShowWindow()
    {
        _window.Show();
        _window.ShowInTaskbar = true;
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = WindowState.Normal;
        _window.Activate();
        _window.Topmost = true;
        _window.Topmost = false;
        _window.Focus();
        Invoke(Resumed);
    }

    public void RequestExit()
    {
        _exiting = true;
        Application.Current.Shutdown();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (_window.WindowState != WindowState.Minimized) return;
        _window.Hide();
        _window.ShowInTaskbar = false;
        Invoke(Suspended);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_exiting) return;

        e.Cancel = true;
        _window.Hide();
        _window.ShowInTaskbar = false;
        Invoke(Suspended);

        if (_firstHideDone) return;
        _firstHideDone = true;
        try { FirstHideNotifier?.Invoke(); }
        catch { /* balloon is best-effort */ }
    }

    private static void Invoke(Action? hook)
    {
        try { hook?.Invoke(); }
        catch { /* suspend/resume hooks are best-effort */ }
    }
}
