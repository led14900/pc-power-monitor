using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.App.ViewModels;

namespace PcPowerMonitor.App.Views;

/// <summary>
/// Modal "Giới thiệu" window. Opened from Settings via <c>new AboutView().ShowDialog()</c>
/// — no DI, its <see cref="AboutViewModel"/> is a plain object.
/// </summary>
public partial class AboutView : Window
{
    private readonly ILogger? _log;

    public AboutView(ILogger? log = null)
    {
        _log = log;
        InitializeComponent();
        DataContext = new AboutViewModel();
    }

    private void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "Mở liên kết mã nguồn lỗi.");
            MessageBox.Show(this, "Không mở được liên kết: " + ex.Message, "PC Power Monitor",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
