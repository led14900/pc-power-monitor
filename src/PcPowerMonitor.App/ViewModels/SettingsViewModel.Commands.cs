using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.App.Views;
using PcPowerMonitor.Core.Infrastructure;
using PcPowerMonitor.Core.Settings;
using PcPowerMonitor.Core.Startup;

namespace PcPowerMonitor.App.ViewModels;

/// <summary>Command surface for <see cref="SettingsViewModel"/>.</summary>
public sealed partial class SettingsViewModel
{
    [RelayCommand]
    private void Save()
    {
        try
        {
            _store.Save(BuildSettings());
            LoadFromStore();
            StatusMessage = "Đã lưu cài đặt.";
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Lưu cài đặt lỗi.");
            StatusMessage = "Lưu cài đặt thất bại: " + ex.Message;
        }
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        if (MessageBox.Show(
                "Khôi phục toàn bộ cài đặt về mặc định?", "Xác nhận",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            _store.Save(new AppSettings());
            LoadFromStore();
            StatusMessage = "Đã khôi phục cài đặt mặc định.";
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Khôi phục mặc định lỗi.");
            StatusMessage = "Khôi phục thất bại: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task CleanupNowAsync()
    {
        try
        {
            // Persist the current UI value first so the retention pass (which now reads
            // the settings store live) uses exactly what the user sees — no divergence
            // between this button and the scheduled 24h pass.
            Save();
            await _retention.RunAsync().ConfigureAwait(true);
            RefreshDbSize();
            StatusMessage = "Đã dọn dẹp dữ liệu cũ.";
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Dọn dẹp dữ liệu lỗi.");
            StatusMessage = "Dọn dẹp thất bại: " + ex.Message;
        }
    }

    [RelayCommand]
    private void OpenDataFolder() => OpenFolder(AppPaths.DataDir);

    [RelayCommand]
    private void OpenLogFolder() => OpenFolder(AppPaths.LogsDir);

    [RelayCommand]
    private void TestNotification()
    {
        _notifier.Notify("PC Power Monitor", "Đây là thông báo thử nghiệm.", "Info");
        StatusMessage = "Đã gửi thông báo thử.";
    }

    [RelayCommand]
    private void OpenAbout()
    {
        try
        {
            var view = new AboutView(_log) { Owner = Application.Current?.MainWindow };
            view.ShowDialog();
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Mở màn hình Giới thiệu lỗi.");
            StatusMessage = "Không mở được màn hình Giới thiệu.";
        }
    }

    /// <summary>
    /// Checkbox-driven: reconcile the Windows scheduled task immediately. On failure show
    /// the Vietnamese reason and revert the checkbox without re-entering this handler.
    /// </summary>
    partial void OnAutoStartEnabledChanged(bool value)
    {
        if (_loading || _revertingAutoStart) return;

        try
        {
            if (value) _autoStart.Enable(); else _autoStart.Disable();
            StatusMessage = value
                ? "Đã bật khởi động cùng Windows."
                : "Đã tắt khởi động cùng Windows.";
        }
        catch (AutoStartException ex)
        {
            _log?.LogWarning(ex, "Đổi trạng thái khởi động cùng Windows lỗi.");
            StatusMessage = ex.Message;
            _revertingAutoStart = true;
            AutoStartEnabled = !value;
            _revertingAutoStart = false;
        }
    }

    private void OpenFolder(string path)
    {
        try
        {
            AppPaths.EnsureCreated();
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Mở thư mục lỗi.");
            StatusMessage = "Không mở được thư mục.";
        }
    }
}
