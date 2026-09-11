using System.Threading;

namespace PcPowerMonitor.App.Ui;

/// <summary>
/// Throttle-latest: forwards the newest pushed value at most once per
/// <c>minInterval</c>. The first push in an idle window fires immediately (leading
/// edge); pushes inside the window overwrite a single pending slot and are flushed
/// once the window closes (trailing edge). Intermediate values are dropped.
/// WPF-free — the emit callback is responsible for marshalling onto the Dispatcher.
/// </summary>
public sealed class UiThrottle<T> : IDisposable
{
    private readonly long _minTicks;
    private readonly Action<T> _emit;
    private readonly Func<long> _nowTicks;
    private readonly Timer _timer;
    private readonly object _gate = new();

    private T _pending = default!;
    private bool _hasPending;
    private long _lastEmitTicks;
    private bool _disposed;

    public UiThrottle(TimeSpan minInterval, Action<T> onEmit, Func<long>? nowTicks = null)
    {
        _minTicks = Math.Max(1, minInterval.Ticks);
        _emit = onEmit ?? throw new ArgumentNullException(nameof(onEmit));
        _nowTicks = nowTicks ?? (() => Environment.TickCount64 * TimeSpan.TicksPerMillisecond);
        _lastEmitTicks = _nowTicks() - _minTicks; // allow an immediate first emit
        _timer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Push(T value)
    {
        bool emitNow;
        lock (_gate)
        {
            if (_disposed) return;
            _pending = value;
            _hasPending = true;

            var elapsed = _nowTicks() - _lastEmitTicks;
            emitNow = elapsed >= _minTicks;
            if (emitNow)
            {
                _hasPending = false;
                _lastEmitTicks = _nowTicks();
            }
            else
            {
                var due = (_minTicks - elapsed) / TimeSpan.TicksPerMillisecond;
                _timer.Change(Math.Max(1, due), Timeout.Infinite);
            }
        }

        if (emitNow) _emit(value);
    }

    /// <summary>Emit the pending value now, if any. Invoked by the trailing-edge timer.</summary>
    public void Flush()
    {
        T value;
        lock (_gate)
        {
            if (_disposed || !_hasPending) return;
            value = _pending;
            _hasPending = false;
            _lastEmitTicks = _nowTicks();
        }
        _emit(value);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _timer.Dispose();
    }
}
