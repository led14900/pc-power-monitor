using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.App.Tray;
using PcPowerMonitor.App.ViewModels;
using PcPowerMonitor.Core.Infrastructure;

namespace PcPowerMonitor.App;

public partial class App : Application
{
    private const string AutoStartSwitch = "--autostart";
    private static readonly TimeSpan AutoStartDelay = TimeSpan.FromSeconds(15);

    private IHost? _host;
    private SingleInstanceGuard? _instanceGuard;
    private TrayIconHost? _tray;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ApplyVietnameseCulture();

        _instanceGuard = new SingleInstanceGuard();
        if (!_instanceGuard.IsOwner)
        {
            // A copy is already running — wake it and bow out.
            SingleInstanceGuard.SignalExistingInstance();
            _instanceGuard.Dispose();
            _instanceGuard = null;
            Shutdown();
            return;
        }

        // Closing / minimising the main window must not quit the app (tray keeps it alive).
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        var startHidden = e.Args.Any(
            a => string.Equals(a, AutoStartSwitch, StringComparison.OrdinalIgnoreCase));

        try
        {
            _host = HostBuilderExtensions.BuildAppHost();
            await _host.StartAsync();

            var log = _host.Services.GetRequiredService<ILogger<App>>();
            log.LogInformation("App started (elevated={Elevated}, autostart={Auto}).",
                ElevationChecker.IsElevated(), startHidden);

            var visibility = _host.Services.GetRequiredService<WindowVisibilityService>();
            _tray = _host.Services.GetRequiredService<TrayIconHost>();
            _tray.Initialize();

            var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
            mainViewModel.Initialize();
            visibility.Suspended = mainViewModel.Suspend;
            visibility.Resumed = mainViewModel.Resume;

            _instanceGuard.StartShowListener(() => Dispatcher.Invoke(visibility.ShowWindow));

            if (startHidden)
            {
                mainViewModel.Suspend();
                log.LogInformation("Auto-start: chờ {Seconds}s rồi chạy nền (chỉ tray).",
                    AutoStartDelay.TotalSeconds);
                await Task.Delay(AutoStartDelay);
            }
            else
            {
                visibility.ShowWindow();
            }
        }
        catch (Exception ex)
        {
            // Must leave a trace: a startup failure otherwise vanishes with the MessageBox.
            LogFatal(ex);
            MessageBox.Show(
                $"Ứng dụng không khởi động được: {ex.Message}\n\nXem log tại:\n{AppPaths.LogsDir}",
                "PC Power Monitor", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>Pin the process + WPF to vi-VN so number/currency <c>StringFormat</c>
    /// bindings render <c>1.234,5</c> / <c>12.345 ₫</c> regardless of the OS locale.</summary>
    private static void ApplyVietnameseCulture()
    {
        var vi = CultureInfo.GetCultureInfo("vi-VN");
        CultureInfo.DefaultThreadCurrentCulture = vi;
        CultureInfo.DefaultThreadCurrentUICulture = vi;
        Thread.CurrentThread.CurrentCulture = vi;
        Thread.CurrentThread.CurrentUICulture = vi;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(vi.IetfLanguageTag)));
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();

        if (_host is not null)
        {
            using (_host)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
            }
        }

        _instanceGuard?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogFatal(e.Exception);
        MessageBox.Show(
            $"Đã xảy ra lỗi: {e.Exception.Message}\n\nXem chi tiết trong log:\n{AppPaths.LogsDir}",
            "PC Power Monitor", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LogFatal(ex);
    }

    private void LogFatal(Exception ex)
    {
        try
        {
            _host?.Services.GetRequiredService<ILogger<App>>()
                .LogCritical(ex, "Unhandled exception");
        }
        catch
        {
            // Logging must never throw from the last-resort handler.
        }

        // The host logger is gone if startup failed before Build(); Serilog's static
        // sink is wired in AddAppFileLogging() and survives that, so always try it too.
        try
        {
            Serilog.Log.Fatal(ex, "Unhandled exception (static sink).");
        }
        catch
        {
            // ditto
        }
    }
}
