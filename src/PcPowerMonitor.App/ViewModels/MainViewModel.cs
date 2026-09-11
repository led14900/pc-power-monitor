using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.App.Ui;
using PcPowerMonitor.Core.Hardware;
using PcPowerMonitor.Core.Models;
using PcPowerMonitor.Core.Sampling;
using PcPowerMonitor.Core.Storage;

namespace PcPowerMonitor.App.ViewModels;

/// <summary>
/// Shell view-model. Subscribes to <see cref="ISnapshotBroadcaster"/> once and fans
/// each tick into three throttle-latest cadences (1s numbers / 2s hardware table /
/// 5s chart append), always marshalling property writes onto the Dispatcher.
/// <see cref="Suspend"/> / <see cref="Resume"/> stop all work while the window is
/// hidden to tray.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private const string SensorHelpUrl =
        "https://github.com/anthropics/pc-power-monitor/blob/main/docs/sensors.md";
    private static readonly TimeSpan SummaryInterval = TimeSpan.FromSeconds(60);

    private readonly ISnapshotBroadcaster _broadcaster;
    private readonly IEnergyQueryService _query;
    private readonly ILogger<MainViewModel>? _log;
    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;

    private readonly UiThrottle<SampleTick> _numbersThrottle;
    private readonly UiThrottle<SampleTick> _hardwareThrottle;
    private readonly UiThrottle<SampleTick> _chartThrottle;
    private readonly Timer _summaryTimer;

    private readonly object _kwhGate = new();
    private double _pendingKwh;
    private volatile bool _suspended;
    private bool _disposed;

    public MainViewModel(
        ISnapshotBroadcaster broadcaster,
        IEnergyQueryService query,
        DashboardViewModel dashboard,
        HardwareViewModel hardware,
        PowerChartViewModel chart,
        ReportViewModel report,
        SettingsViewModel settings,
        ILogger<MainViewModel>? log = null)
    {
        _broadcaster = broadcaster;
        _query = query;
        Dashboard = dashboard;
        Hardware = hardware;
        Chart = chart;
        Report = report;
        Settings = settings;
        _log = log;

        _numbersThrottle = new UiThrottle<SampleTick>(TimeSpan.FromSeconds(1), EmitNumbers);
        _hardwareThrottle = new UiThrottle<SampleTick>(TimeSpan.FromSeconds(2), EmitHardware);
        _chartThrottle = new UiThrottle<SampleTick>(TimeSpan.FromSeconds(5), EmitChart);
        _summaryTimer = new Timer(_ => RefreshSummary(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public DashboardViewModel Dashboard { get; }

    public HardwareViewModel Hardware { get; }

    public PowerChartViewModel Chart { get; }

    public ReportViewModel Report { get; }

    public SettingsViewModel Settings { get; }

    [ObservableProperty] private int selectedTabIndex;
    [ObservableProperty] private bool isDegraded;

    [ObservableProperty]
    private string degradedMessage =
        "Không đọc được cảm biến phần cứng. Chạy bằng quyền Administrator hoặc kiểm tra Windows Defender.";

    /// <summary>Wire up the broadcast subscription and kick off the first loads.</summary>
    public void Initialize()
    {
        _broadcaster.Ticked += OnTicked;
        _summaryTimer.Change(TimeSpan.Zero, SummaryInterval);
        _ = Chart.SetRangeCommand.ExecuteAsync(ChartRange.LastHour);
    }

    public void Suspend()
    {
        _suspended = true;
        _summaryTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public void Resume()
    {
        if (_disposed) return;
        _suspended = false;
        _summaryTimer.Change(TimeSpan.Zero, SummaryInterval);
        _ = Chart.LoadAsync();
    }

    private void OnTicked(SampleTick tick)
    {
        if (_suspended) return;
        lock (_kwhGate) _pendingKwh += tick.Increment.Kwh;
        _numbersThrottle.Push(tick);
        _hardwareThrottle.Push(tick);
        _chartThrottle.Push(tick);
    }

    private void EmitNumbers(SampleTick tick) => Marshal(() =>
    {
        double delta;
        lock (_kwhGate) { delta = _pendingKwh; _pendingKwh = 0d; }
        Dashboard.ApplyTick(tick.Estimate, delta);
        UpdateDegraded(tick.Snapshot);
    });

    private void EmitHardware(SampleTick tick) => Marshal(() => Hardware.ApplySnapshot(tick.Snapshot));

    private void EmitChart(SampleTick tick) => Marshal(
        () => Chart.AppendLatest(tick.Snapshot.TimestampUtc, tick.Estimate.WallW));

    private void UpdateDegraded(HardwareSnapshot snapshot)
    {
        IsDegraded = snapshot.Availability == SensorAvailability.Unavailable;
        if (!string.IsNullOrWhiteSpace(snapshot.AvailabilityReason))
        {
            DegradedMessage = snapshot.AvailabilityReason!;
            Hardware.EmptyMessage = snapshot.AvailabilityReason!;
        }
    }

    private void RefreshSummary()
    {
        if (_suspended) return;
        try
        {
            var summary = _query.GetTodaySummary();
            Marshal(() => Dashboard.ApplySummary(summary));
        }
        catch (Exception ex)
        {
            _log?.LogDebug(ex, "Làm mới tổng hợp hôm nay lỗi.");
        }
    }

    private void Marshal(Action action)
    {
        if (_suspended || _disposed) return;
        _dispatcher.InvokeAsync(action, DispatcherPriority.Background);
    }

    [RelayCommand]
    private void OpenGuide()
    {
        try
        {
            Process.Start(new ProcessStartInfo(SensorHelpUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Mở hướng dẫn cảm biến lỗi.");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _broadcaster.Ticked -= OnTicked;
        _summaryTimer.Dispose();
        _numbersThrottle.Dispose();
        _hardwareThrottle.Dispose();
        _chartThrottle.Dispose();
    }
}
