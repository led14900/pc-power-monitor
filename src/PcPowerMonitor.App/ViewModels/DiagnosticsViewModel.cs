using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.Core.Hardware;
using PcPowerMonitor.Core.Infrastructure;

namespace PcPowerMonitor.App.ViewModels;

/// <summary>
/// "Chẩn đoán" group of the Settings screen — the primary remote-support tool. Dumps
/// the sensor reader's diagnostics, availability state, log-folder path and DB size,
/// and lets the user copy it all to the clipboard.
/// </summary>
public sealed partial class DiagnosticsViewModel : ObservableObject
{
    private readonly IHardwareSensorReader _reader;
    private readonly ILogger<DiagnosticsViewModel>? _log;

    public DiagnosticsViewModel(IHardwareSensorReader reader, ILogger<DiagnosticsViewModel>? log = null)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _log = log;
        Refresh();
    }

    public ObservableCollection<string> DiagnosticLines { get; } = new();

    [ObservableProperty] private string availability = string.Empty;
    [ObservableProperty] private string? availabilityReason;
    [ObservableProperty] private string logFolderPath = string.Empty;
    [ObservableProperty] private double databaseSizeMb;
    [ObservableProperty] private string diagnosticsText = string.Empty;

    [RelayCommand]
    private void Refresh()
    {
        try
        {
            Availability = _reader.Availability.ToString();
            AvailabilityReason = _reader.AvailabilityReason;

            DiagnosticLines.Clear();
            foreach (var line in _reader.GetDiagnostics()) DiagnosticLines.Add(line);

            LogFolderPath = AppPaths.LogsDir;
            var db = new FileInfo(AppPaths.DatabaseFile);
            DatabaseSizeMb = db.Exists ? Math.Round(db.Length / 1_048_576d, 2) : 0d;

            DiagnosticsText = BuildText();
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Làm mới chẩn đoán lỗi.");
        }
    }

    [RelayCommand]
    private void CopyDiagnostics()
    {
        try
        {
            Clipboard.SetText(string.IsNullOrEmpty(DiagnosticsText) ? "(không có dữ liệu)" : DiagnosticsText);
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Sao chép chẩn đoán vào clipboard lỗi.");
        }
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            AppPaths.EnsureCreated();
            Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.LogsDir) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Mở thư mục log lỗi.");
        }
    }

    private string BuildText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Trạng thái cảm biến: {Availability}");
        if (!string.IsNullOrWhiteSpace(AvailabilityReason)) sb.AppendLine($"Lý do: {AvailabilityReason}");
        sb.AppendLine($"Thư mục log: {LogFolderPath}");
        sb.AppendLine($"Dung lượng CSDL: {DatabaseSizeMb:0.##} MB");
        sb.AppendLine("--- Cảm biến phát hiện được ---");
        foreach (var line in DiagnosticLines) sb.AppendLine(line);
        return sb.ToString();
    }
}
