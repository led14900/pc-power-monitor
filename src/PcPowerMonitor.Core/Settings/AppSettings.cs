using System.Text.Json.Serialization;
using PcPowerMonitor.Core.Billing;
using PcPowerMonitor.Core.Power;

namespace PcPowerMonitor.Core.Settings;

/// <summary>
/// The single on-disk configuration object (<c>settings.json</c> in
/// <c>%LOCALAPPDATA%\PcPowerMonitor</c>). One root record composed of small section
/// records so every consumer (sampling loop, power estimator, tariff provider, alert
/// service, retention) reads one source of truth. Every field has a sane default so a
/// fresh install works with no file present.
/// </summary>
public sealed record AppSettings
{
    /// <summary>Bumped when the shape changes so a future load can migrate old files.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>JSON has no comments; this human note rides along instead.</summary>
    [JsonPropertyName("_note")]
    public string Note { get; init; } =
        "Cấu hình PC Power Monitor. Có thể sửa bằng tay; giá trị ngoài khoảng hợp lệ " +
        "sẽ tự được điều chỉnh khi tải.";

    public SamplingSettings Sampling { get; init; } = new();

    /// <summary>Reuses the Power-layer record verbatim — no parallel definition.</summary>
    public HardwareProfile HardwareProfile { get; init; } = new();

    /// <summary>Reuses the Billing-layer record verbatim.</summary>
    public TariffSettings Tariff { get; init; } = new();

    public StorageSettings Storage { get; init; } = new();

    public StartupSettings Startup { get; init; } = new();

    public AlertSettings Alerts { get; init; } = new();

    public UiSettings Ui { get; init; } = new();

    /// <summary>Optional AI-assisted hardware profile lookup (Vertex AI). See <see cref="AiSettings"/>.</summary>
    public AiSettings Ai { get; init; } = new();
}
