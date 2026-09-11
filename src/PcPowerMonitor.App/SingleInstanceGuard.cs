using System.Threading;

namespace PcPowerMonitor.App;

/// <summary>
/// Enforces a single running instance. The first process owns a global
/// <see cref="Mutex"/> and listens on a named <see cref="EventWaitHandle"/>; a second
/// process fails to acquire the mutex, <see cref="SignalExistingInstance"/>s the first
/// one (which then shows its window) and exits.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = "Global\\PcPowerMonitor";
    private const string ShowSignalName = "PcPowerMonitor.Show";

    private readonly Mutex _mutex;
    private EventWaitHandle? _showSignal;
    private Thread? _listener;
    private volatile bool _running;
    private bool _disposed;

    /// <summary>True when this process is the first / owning instance.</summary>
    public bool IsOwner { get; }

    public SingleInstanceGuard()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsOwner = createdNew;
    }

    /// <summary>Second instance: wake the first one. Safe no-op if it already exited.</summary>
    public static void SignalExistingInstance()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(ShowSignalName, out var handle))
            {
                using (handle)
                    handle.Set();
            }
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // First instance is gone; nothing to signal.
        }
    }

    /// <summary>
    /// Owner only: spin up a background thread that invokes <paramref name="onShow"/>
    /// whenever a later instance signals. <paramref name="onShow"/> must marshal to the
    /// UI thread itself.
    /// </summary>
    public void StartShowListener(Action onShow)
    {
        ArgumentNullException.ThrowIfNull(onShow);
        if (!IsOwner) return;

        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
        _running = true;
        _listener = new Thread(() => ListenLoop(onShow))
        {
            IsBackground = true,
            Name = "SingleInstanceShowListener",
        };
        _listener.Start();
    }

    private void ListenLoop(Action onShow)
    {
        while (_running)
        {
            try
            {
                if (_showSignal!.WaitOne(1000) && _running)
                    onShow();
            }
            catch
            {
                // Never let the listener thread die on a transient failure.
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _running = false;
        try { _listener?.Join(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
        _showSignal?.Dispose();

        try { if (IsOwner) _mutex.ReleaseMutex(); } catch { /* not owned on this thread */ }
        _mutex.Dispose();
    }
}
