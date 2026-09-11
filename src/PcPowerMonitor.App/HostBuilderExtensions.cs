using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PcPowerMonitor.App.Tray;
using PcPowerMonitor.App.ViewModels;
using PcPowerMonitor.App.Views;
using PcPowerMonitor.Core.Alerts;
using PcPowerMonitor.Core.Billing;
using PcPowerMonitor.Core.Billing.Reporting;
using PcPowerMonitor.Core.Hardware;
using PcPowerMonitor.Core.Infrastructure;
using PcPowerMonitor.Core.Power;
using PcPowerMonitor.Core.Sampling;
using PcPowerMonitor.Core.Settings;
using PcPowerMonitor.Core.Startup;
using PcPowerMonitor.Core.Storage;

namespace PcPowerMonitor.App;

/// <summary>
/// The one place where the DI container is configured. Later phases add their
/// service registrations here and nowhere else.
/// </summary>
public static class HostBuilderExtensions
{
    public static IHost BuildAppHost()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddAppFileLogging();
        ConfigureAppServices(builder.Services);

        var host = builder.Build();

        // Bring the database to the current schema version before any hosted service
        // (sampling loop, rollup timer) touches it.
        host.Services.GetRequiredService<DatabaseMigrator>().Migrate();

        // Force the settings store to load (and create settings.json if missing) before
        // hosted services start, so the first sample already uses the user's config.
        host.Services.GetRequiredService<ISettingsStore>();

        return host;
    }

    /// <summary>Register application services. Extended by every subsequent phase.</summary>
    public static void ConfigureAppServices(IServiceCollection services)
    {
        // Views + view-models (phase 06). MainViewModel is a singleton (owns the
        // broadcast subscription + throttles); tab VMs are transient instances that
        // it holds for the process lifetime.
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<HardwareViewModel>();
        services.AddTransient<PowerChartViewModel>();
        services.AddTransient<ReportViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<DiagnosticsViewModel>();
        services.AddTransient<HardwareProfileSectionViewModel>();
        services.AddTransient<TariffSectionViewModel>();

        // AI-assisted hardware lookup (Vertex AI service account — the only AI
        // provider). AiSectionViewModel MUST be a singleton: both SettingsViewModel
        // (the "Cấu hình AI" section) and HardwareProfileSectionViewModel (the lookup
        // button/flow) need the exact same instance so service-account/model/machine-model
        // edits are visible to the lookup flow without a save round-trip. The lookup
        // flow itself builds its IAiClient on demand via the static AiClientFactory, so
        // it does NOT take an IAiClient constructor dependency.
        services.AddSingleton<AiSectionViewModel>();

        // Hardware sensor source. Singleton: LHM is not thread-safe and holds a
        // kernel driver handle for the whole process lifetime.
        services.AddSingleton<IHardwareSensorReader, LhmSensorReader>();

        // Power estimation + energy integration (phase 03). EnergyAccumulator is a
        // singleton so kWh accumulates for the whole process; its clock is monotonic.
        services.AddSingleton<IElapsedClock, StopwatchElapsedClock>();
        services.AddSingleton<PowerEstimator>();
        services.AddSingleton<EnergyAccumulator>();

        // Storage (phase 04). All singletons: the writer holds one long-lived
        // connection; rollup/retention/query open their own short-lived ones.
        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<DatabaseMigrator>();
        services.AddSingleton<ISampleWriter, BatchSampleWriter>();
        services.AddSingleton<RollupService>();
        services.AddSingleton<RetentionService>();
        services.AddSingleton<IEnergyQueryService, EnergyQueryService>();

        // Unified JSON settings store (phase 08). Singleton; eagerly loaded in
        // BuildAppHost before any hosted service starts.
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();

        // EVN electricity billing + reports (phase 07). One ElectricityCostCalculator
        // serves both the pure billing interface and the rollup pipeline's ICostCalculator
        // (the real EVN cost, replacing the phase-04 zero-cost placeholder). Phase 08
        // rebinds the tariff source from options to the JSON settings store.
        services.AddSingleton<ITariffProvider, SettingsStoreTariffProvider>();
        services.AddSingleton<ElectricityCostCalculator>();
        services.AddSingleton<IElectricityCostCalculator>(sp => sp.GetRequiredService<ElectricityCostCalculator>());
        services.AddSingleton<ICostCalculator>(sp => sp.GetRequiredService<ElectricityCostCalculator>());
        services.AddSingleton<IEnergyReportService, EnergyReportService>();
        services.AddSingleton<ICsvExporter, CsvExporter>();

        // Sampling loop + background maintenance + system power events (phase 05).
        services.AddSingleton<SamplingOptions>();
        services.AddSingleton<ISnapshotBroadcaster, SnapshotBroadcaster>();
        services.AddHostedService<SamplingHostedService>();
        services.AddHostedService<MaintenanceHostedService>();
        services.AddHostedService<SystemPowerEventListener>();

        // Windows auto-start (schtasks) — driven from Settings in phase 08.
        services.AddSingleton<IAutoStartManager, TaskSchedulerAutoStartManager>();

        // Threshold alerts (phase 08). AlertService is resolvable directly (the Settings
        // screen reads its alert log) and also runs as a hosted service.
        services.AddSingleton<ITrayNotifier, TrayNotifier>();
        services.AddSingleton<AlertService>();
        services.AddHostedService(sp => sp.GetRequiredService<AlertService>());

        // Tray + window-to-tray behaviour. Created explicitly from App.OnStartup.
        services.AddSingleton<WindowVisibilityService>();
        services.AddSingleton<TrayIconHost>();

        // TrayNotifier needs the tray icon only at alert time; a Lazy handle keeps the
        // heavy TrayIconHost graph out of its constructor and breaks the DI cycle
        // (AlertService/SettingsViewModel → ITrayNotifier → TrayIconHost → MainViewModel).
        services.AddSingleton(sp => new Lazy<TrayIconHost>(sp.GetRequiredService<TrayIconHost>));

        // ViewModels / services are added by later phases.
    }
}
