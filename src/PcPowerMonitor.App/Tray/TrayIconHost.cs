using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using H.NotifyIcon;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.Core.Sampling;
using PcPowerMonitor.Core.Storage;

namespace PcPowerMonitor.App.Tray;

/// <summary>
/// Owns the H.NotifyIcon <see cref="TaskbarIcon"/>: builds it in code, wires the
/// context menu to <see cref="TrayMenuViewModel"/> and refreshes the tooltip from
/// broadcast ticks — throttled to once every 5s to avoid flicker and CPU churn.
/// Missing / unloadable icon degrades gracefully (default icon, no crash).
/// </summary>
public sealed class TrayIconHost : IDisposable
{
    private static readonly TimeSpan TooltipInterval = TimeSpan.FromSeconds(5);
    private const string IconResourceUri = "pack://application:,,,/Assets/tray-icon.ico";

    private readonly ISnapshotBroadcaster _broadcaster;
    private readonly IEnergyQueryService _query;
    private readonly ICostCalculator _cost;
    private readonly WindowVisibilityService _visibility;
    private readonly SamplingOptions _sampling;
    private readonly ILogger<TrayIconHost>? _log;
    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;

    private TaskbarIcon? _icon;
    private long _lastTooltipTicks;
    private bool _disposed;

    /// <summary>The live tray icon, or null before <see cref="Initialize"/> / after dispose.
    /// Exposed so <see cref="TrayNotifier"/> can raise alert notifications through it.</summary>
    public TaskbarIcon? Icon => _icon;

    public TrayIconHost(
        ISnapshotBroadcaster broadcaster,
        IEnergyQueryService query,
        ICostCalculator cost,
        WindowVisibilityService visibility,
        SamplingOptions sampling,
        ILogger<TrayIconHost>? log = null)
    {
        _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
        _query = query ?? throw new ArgumentNullException(nameof(query));
        _cost = cost ?? throw new ArgumentNullException(nameof(cost));
        _visibility = visibility ?? throw new ArgumentNullException(nameof(visibility));
        _sampling = sampling ?? throw new ArgumentNullException(nameof(sampling));
        _log = log;
    }

    /// <summary>Create the tray icon. Call once, on the UI thread, after the host starts.</summary>
    public void Initialize()
    {
        var menu = new TrayMenuViewModel(
            _sampling,
            showWindow: _visibility.ShowWindow,
            openSettings: OnOpenSettings,
            exit: _visibility.RequestExit);

        _icon = new TaskbarIcon
        {
            ToolTipText = "PC Power Monitor",
            ContextMenu = BuildContextMenu(menu),
            Visibility = Visibility.Visible,
        };

        TrySetIconImage(_icon);
        _icon.TrayMouseDoubleClick += (_, _) => _visibility.ShowWindow();

        try { _icon.ForceCreate(); }
        catch (Exception ex) { _log?.LogWarning(ex, "ForceCreate tray icon lỗi."); }

        _visibility.FirstHideNotifier = ShowStillRunningBalloon;
        _broadcaster.Ticked += OnTicked;
    }

    private static ContextMenu BuildContextMenu(TrayMenuViewModel vm)
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "Mở cửa sổ", Command = vm.ShowWindowCommand });

        var pause = new MenuItem { Command = vm.TogglePauseCommand };
        pause.SetBinding(HeaderedItemsControl.HeaderProperty,
            new Binding(nameof(TrayMenuViewModel.PauseLabel)) { Source = vm });
        menu.Items.Add(pause);

        menu.Items.Add(new MenuItem { Header = "Cài đặt", Command = vm.OpenSettingsCommand });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "Thoát", Command = vm.ExitCommand });
        return menu;
    }

    private void TrySetIconImage(TaskbarIcon icon)
    {
        try
        {
            icon.IconSource = new BitmapImage(new Uri(IconResourceUri, UriKind.Absolute));
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Không nạp được tray-icon.ico; dùng icon mặc định.");
        }
    }

    private void OnTicked(SampleTick tick)
    {
        var now = DateTime.UtcNow.Ticks;
        var last = Interlocked.Read(ref _lastTooltipTicks);
        if (now - last < TooltipInterval.Ticks) return;
        Interlocked.Exchange(ref _lastTooltipTicks, now);

        var text = BuildTooltip(tick);
        _dispatcher.BeginInvoke(() =>
        {
            if (_icon is not null) _icon.ToolTipText = text;
        });
    }

    private string BuildTooltip(SampleTick tick)
    {
        try
        {
            var today = _query.GetTodaySummary();
            var vnd = _cost.Calculate(today.Kwh, DateOnly.FromDateTime(DateTime.Now)).CostVnd;
            return $"{tick.Estimate.WallW:0} W | {today.Kwh:0.00} kWh hôm nay | {vnd:N0} đ";
        }
        catch (Exception ex)
        {
            _log?.LogDebug(ex, "Dựng tooltip tray lỗi.");
            return $"{tick.Estimate.WallW:0} W";
        }
    }

    private void ShowStillRunningBalloon()
    {
        _dispatcher.BeginInvoke(() =>
        {
            try
            {
                _icon?.ShowNotification(
                    title: "PC Power Monitor",
                    message: "Ứng dụng vẫn chạy nền để tiếp tục ghi điện năng. " +
                             "Chọn \"Thoát\" trong menu khay hệ thống để đóng hẳn.");
            }
            catch (Exception ex)
            {
                _log?.LogDebug(ex, "Hiển thị balloon tray lỗi.");
            }
        });
    }

    private void OnOpenSettings()
    {
        _log?.LogInformation("Menu 'Cài đặt' được chọn (màn hình Settings có ở phase 08).");
        _visibility.ShowWindow();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _broadcaster.Ticked -= OnTicked;
        _visibility.FirstHideNotifier = null;
        _icon?.Dispose();
        _icon = null;
    }
}
