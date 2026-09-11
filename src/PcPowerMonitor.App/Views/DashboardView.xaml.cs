using System.Windows.Controls;

namespace PcPowerMonitor.App.Views;

/// <summary>"Tổng quan" tab. DataContext is <c>MainViewModel</c>; bindings reach the
/// dashboard + chart child view-models through it.</summary>
public partial class DashboardView : UserControl
{
    public DashboardView() => InitializeComponent();
}
