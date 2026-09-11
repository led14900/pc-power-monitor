using System.Windows.Controls;

namespace PcPowerMonitor.App.Views;

/// <summary>"Chẩn đoán" group. DataContext is <c>DiagnosticsViewModel</c>
/// (via <c>SettingsViewModel.Diagnostics</c>).</summary>
public partial class DiagnosticsView : UserControl
{
    public DiagnosticsView() => InitializeComponent();
}
