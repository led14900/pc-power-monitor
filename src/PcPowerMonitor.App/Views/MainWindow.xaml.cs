using System.Windows;
using PcPowerMonitor.App.ViewModels;

namespace PcPowerMonitor.App.Views;

/// <summary>Shell window: a degraded-mode banner over a 4-tab <c>TabControl</c>.
/// "Báo cáo" hosts <c>ReportView</c> (phase 07); "Cài đặt" hosts <c>SettingsView</c> (phase 08).</summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
