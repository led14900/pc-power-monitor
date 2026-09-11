using System.IO;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace PcPowerMonitor.App.ViewModels;

/// <summary>Export (CSV / PNG) and history-recalculation commands for the report tab.</summary>
public sealed partial class ReportViewModel
{
    private static string DocumentsDir =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    [RelayCommand]
    private void ExportCsv()
    {
        if (Rows.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"bao-cao-dien-{SelectedYear}.csv",
            InitialDirectory = DocumentsDir,
            AddExtension = true,
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            _csv.WriteToFile(dialog.FileName, SelectedYear, Rows.ToList(), _tariff.Current);
            _log?.LogInformation("Đã xuất CSV báo cáo năm {Year}: {Path}", SelectedYear, dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.LogWarning(ex, "Xuất CSV lỗi: {Path}", dialog.FileName);
            MessageBox.Show(
                "Không ghi được file. Có thể file đang mở trong Excel.",
                "Xuất CSV", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private void ExportChartPng()
    {
        if (ChartPngSaver is null || Rows.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Filter = "PNG (*.png)|*.png",
            FileName = $"bieu-do-dien-{SelectedYear}.png",
            InitialDirectory = DocumentsDir,
            AddExtension = true,
        };
        if (dialog.ShowDialog() != true) return;

        var error = ChartPngSaver(dialog.FileName);
        if (error is null)
        {
            _log?.LogInformation("Đã xuất ảnh biểu đồ: {Path}", dialog.FileName);
            return;
        }

        _log?.LogWarning("Xuất ảnh biểu đồ lỗi: {Error}", error);
        MessageBox.Show(
            $"Không lưu được ảnh biểu đồ.\n{error}",
            "Xuất ảnh", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    [RelayCommand]
    private async Task RecalculateHistoryAsync()
    {
        var confirm = MessageBox.Show(
            "Tính lại toàn bộ lịch sử tiền điện theo đơn giá hiện tại?\n\n" +
            "Thao tác này GHI ĐÈ chi phí đã lưu của mọi ngày/tháng bằng đơn giá và VAT " +
            "hiện hành. Không thể hoàn tác. Chỉ nên dùng khi bạn vừa sửa lại đơn giá cho đúng.",
            "Tính lại lịch sử", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var days = await _report.RecalculateHistoryAsync(_tariff.Current).ConfigureAwait(true);
            _log?.LogInformation("Đã tính lại lịch sử: {Days} ngày.", days);
            MessageBox.Show(
                $"Đã cập nhật {days} ngày theo đơn giá hiện tại.",
                "Tính lại lịch sử", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Tính lại lịch sử lỗi.");
            MessageBox.Show(
                "Không tính lại được lịch sử. Xem log để biết chi tiết.",
                "Tính lại lịch sử", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAsync().ConfigureAwait(true);
    }
}
