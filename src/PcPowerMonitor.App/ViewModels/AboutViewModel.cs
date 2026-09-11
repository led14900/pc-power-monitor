using System.Reflection;

namespace PcPowerMonitor.App.ViewModels;

/// <summary>
/// Backing data for <c>AboutView</c>. Deliberately a plain object (no DI, no
/// <c>ObservableObject</c>): the About window is read-only and short-lived. Shows the
/// app version, the third-party license summary (mirrors <c>THIRD-PARTY-NOTICES.txt</c>),
/// the LibreHardwareMonitor source link and the "estimate, not a wall-socket meter"
/// disclaimer.
/// </summary>
public sealed class AboutViewModel
{
    /// <summary>Canonical LibreHardwareMonitor source URL (MPL 2.0 obligation: point to source).</summary>
    public const string LhmSourceUrl = "https://github.com/LibreHardwareMonitor/LibreHardwareMonitor";

    public AboutViewModel()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        Version = v is null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    /// <summary>App version as <c>major.minor.patch</c>.</summary>
    public string Version { get; }

    public string VersionLine => $"Phiên bản {Version}";

    /// <summary>Shown at the bottom of the window and echoed on the Report screen.</summary>
    public string Disclaimer =>
        "Số liệu là ước tính từ cảm biến phần cứng, không phải thiết bị đo tại ổ cắm.";

    public string SourceLinkText => "Mã nguồn LibreHardwareMonitor";

    public string LhmSource => LhmSourceUrl;

    public string OfflineNote =>
        "Ứng dụng hoạt động hoàn toàn offline: không thu thập telemetry, không gửi dữ liệu ra ngoài.";

    /// <summary>
    /// One line per dependency: name + version + license. Kept in sync with
    /// <c>docs/THIRD-PARTY-NOTICES.txt</c>.
    /// </summary>
    public IReadOnlyList<string> Licenses { get; } = new[]
    {
        "LibreHardwareMonitorLib 0.9.4 — Mozilla Public License 2.0 (liên kết động qua NuGet, không sửa mã nguồn)",
        "ScottPlot / ScottPlot.WPF 5.0.47 — MIT",
        "H.NotifyIcon.Wpf 2.2.0 — MIT",
        "CommunityToolkit.Mvvm 8.3.2 — MIT",
        "Microsoft.Data.Sqlite 8.0.10 — MIT",
        "Microsoft.Extensions.Hosting 8.0.1 — MIT",
        "Microsoft.Win32.SystemEvents 8.0.0 — MIT",
        "Serilog + Serilog.Sinks.File 6.0.0 + Serilog.Extensions.Hosting 8.0.0 — Apache-2.0",
    };
}
