using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PcPowerMonitor.Core.Power;
using PcPowerMonitor.Core.Storage;

namespace PcPowerMonitor.Core.Sampling;

/// <summary>
/// Bridges Windows power/session events into the energy pipeline:
/// <list type="bullet">
///   <item><c>PowerModes.Suspend</c> → flush the sample writer so buffered rows survive hibernate.</item>
///   <item><c>PowerModes.Resume</c> → <c>EnergyAccumulator.MarkGap()</c> so the sleep gap is not billed.</item>
///   <item><c>SessionSwitch</c> (lock/unlock) → log only; the machine keeps running.</item>
/// </list>
/// Handlers run on a non-UI thread and are wrapped in try/catch. <see cref="StopAsync"/>
/// MUST unhook — a dangling <c>SystemEvents</c> handler leaks and can crash on shutdown.
/// </summary>
public sealed class SystemPowerEventListener : IHostedService
{
    private readonly EnergyAccumulator _accumulator;
    private readonly ISampleWriter _writer;
    private readonly ILogger<SystemPowerEventListener>? _log;
    private bool _hooked;

    public SystemPowerEventListener(
        EnergyAccumulator accumulator,
        ISampleWriter writer,
        ILogger<SystemPowerEventListener>? log = null)
    {
        _accumulator = accumulator ?? throw new ArgumentNullException(nameof(accumulator));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _log = log;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        _hooked = true;
        _log?.LogInformation("System power/session event listener attached.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_hooked)
            {
                SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                SystemEvents.SessionSwitch -= OnSessionSwitch;
            }
        }
        finally
        {
            _hooked = false;
        }
        return Task.CompletedTask;
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        try
        {
            switch (e.Mode)
            {
                case PowerModes.Suspend:
                    _log?.LogInformation("Hệ thống chuẩn bị ngủ — flush dữ liệu.");
                    _writer.FlushAsync().GetAwaiter().GetResult();
                    break;
                case PowerModes.Resume:
                    _log?.LogInformation("Hệ thống vừa thức dậy — đánh dấu gap năng lượng.");
                    _accumulator.MarkGap();
                    break;
            }
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Xử lý PowerModeChanged ({Mode}) lỗi.", e.Mode);
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        try
        {
            _log?.LogInformation("SessionSwitch: {Reason} (máy vẫn chạy, không tính gap).", e.Reason);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Xử lý SessionSwitch lỗi.");
        }
    }
}
