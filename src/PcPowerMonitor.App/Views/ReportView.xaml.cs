using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PcPowerMonitor.App.ViewModels;

namespace PcPowerMonitor.App.Views;

/// <summary>
/// "Báo cáo" tab. DataContext is <see cref="ReportViewModel"/> (via
/// <c>MainViewModel.Report</c>). Bridges the VM to the ScottPlot child control: pushes
/// row data into the bar chart on <see cref="ReportViewModel.ReportChanged"/> and lends
/// the VM the chart's PNG saver. Kicks off the first load when shown.
/// </summary>
public partial class ReportView : UserControl
{
    private ReportViewModel? _vm;

    public ReportView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => Detach();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();
        _vm = e.NewValue as ReportViewModel;
        if (_vm is null) return;

        _vm.ReportChanged += OnReportChanged;
        _vm.ChartPngSaver = Chart.TrySavePng;

        if (_vm.LoadCommand.CanExecute(null))
            _ = _vm.LoadCommand.ExecuteAsync(null);
    }

    private void Detach()
    {
        if (_vm is null) return;
        _vm.ReportChanged -= OnReportChanged;
        _vm.ChartPngSaver = null;
        _vm = null;
    }

    private void OnReportChanged(object? sender, EventArgs e)
    {
        if (_vm is null) return;
        var labels = _vm.Rows.Select(r => r.MonthLabel).ToList();
        var values = _vm.Rows.Select(r => r.Kwh).ToList();
        Chart.Render(labels, values);
    }
}
