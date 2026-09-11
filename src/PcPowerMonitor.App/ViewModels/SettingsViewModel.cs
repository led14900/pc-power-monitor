using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.Core.Alerts;
using PcPowerMonitor.Core.Infrastructure;
using PcPowerMonitor.Core.Settings;
using PcPowerMonitor.Core.Startup;
using PcPowerMonitor.Core.Storage;

namespace PcPowerMonitor.App.ViewModels;

/// <summary>
/// "Cài đặt" tab. Loads the unified <see cref="ISettingsStore"/> into editable
/// properties, writes them back (validated + clamped by the store) on Save, and hosts
/// the hardware-profile / tariff / diagnostics sub-view-models plus the live alert log.
/// Commands live in <c>SettingsViewModel.Commands.cs</c>.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly IAutoStartManager _autoStart;
    private readonly RetentionService _retention;
    private readonly ITrayNotifier _notifier;
    private readonly ILogger<SettingsViewModel>? _log;

    private bool _loading;
    private bool _revertingAutoStart;

    public SettingsViewModel(
        ISettingsStore store,
        IAutoStartManager autoStart,
        RetentionService retention,
        ITrayNotifier notifier,
        AlertService alertService,
        HardwareProfileSectionViewModel hardware,
        TariffSectionViewModel tariff,
        AiSectionViewModel ai,
        DiagnosticsViewModel diagnostics,
        ILogger<SettingsViewModel>? log = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _autoStart = autoStart ?? throw new ArgumentNullException(nameof(autoStart));
        _retention = retention ?? throw new ArgumentNullException(nameof(retention));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        _log = log;

        ArgumentNullException.ThrowIfNull(alertService);
        Hardware = hardware ?? throw new ArgumentNullException(nameof(hardware));
        Tariff = tariff ?? throw new ArgumentNullException(nameof(tariff));
        Ai = ai ?? throw new ArgumentNullException(nameof(ai));
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        AlertLog = alertService.Log;

        BindingOperations.EnableCollectionSynchronization(AlertLog, alertService.LogSync);
        LoadFromStore();
    }

    public HardwareProfileSectionViewModel Hardware { get; }

    public TariffSectionViewModel Tariff { get; }

    public AiSectionViewModel Ai { get; }

    public DiagnosticsViewModel Diagnostics { get; }

    public ObservableCollection<AlertLogEntry> AlertLog { get; }

    [ObservableProperty] private int intervalSeconds = 2;
    [ObservableProperty] private int retentionDays = 90;
    [ObservableProperty] private double dbSizeMb;
    [ObservableProperty] private bool autoStartEnabled;
    [ObservableProperty] private bool startMinimized = true;
    [ObservableProperty] private bool alertsEnabled = true;
    [ObservableProperty] private double cpuTempC = 90;
    [ObservableProperty] private double gpuTempC = 90;
    [ObservableProperty] private double powerW = 500;
    [ObservableProperty] private int cooldownMinutes = 10;
    [ObservableProperty] private string? statusMessage;

    private void LoadFromStore()
    {
        _loading = true;
        try
        {
            var s = _store.Current;
            IntervalSeconds = s.Sampling.IntervalSeconds;
            RetentionDays = s.Storage.RetentionDays;
            AutoStartEnabled = s.Startup.AutoStartEnabled;
            StartMinimized = s.Startup.StartMinimized;
            AlertsEnabled = s.Alerts.Enabled;
            CpuTempC = s.Alerts.CpuTempC;
            GpuTempC = s.Alerts.GpuTempC;
            PowerW = s.Alerts.PowerW;
            CooldownMinutes = s.Alerts.CooldownMinutes;
            Hardware.Load(s.HardwareProfile);
            Tariff.Load(s.Tariff);
            Ai.Load(s.Ai);
        }
        finally
        {
            _loading = false;
        }

        RefreshDbSize();
    }

    private AppSettings BuildSettings() => _store.Current with
    {
        Sampling = new SamplingSettings { IntervalSeconds = IntervalSeconds },
        Storage = new StorageSettings { RetentionDays = RetentionDays },
        Startup = new StartupSettings { AutoStartEnabled = AutoStartEnabled, StartMinimized = StartMinimized },
        Alerts = new AlertSettings
        {
            Enabled = AlertsEnabled,
            CpuTempC = CpuTempC,
            GpuTempC = GpuTempC,
            PowerW = PowerW,
            CooldownMinutes = CooldownMinutes,
        },
        HardwareProfile = Hardware.ToProfile(),
        Tariff = Tariff.ToTariff(),
        Ai = Ai.ToSettings(),
    };

    private void RefreshDbSize()
    {
        try
        {
            var db = new FileInfo(AppPaths.DatabaseFile);
            DbSizeMb = db.Exists ? Math.Round(db.Length / 1_048_576d, 2) : 0d;
        }
        catch (Exception ex)
        {
            _log?.LogDebug(ex, "Đọc dung lượng CSDL lỗi.");
            DbSizeMb = 0d;
        }
    }
}
