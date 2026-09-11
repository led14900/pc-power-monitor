using System.Windows;
using System.Windows.Controls;
using PcPowerMonitor.App.ViewModels;

namespace PcPowerMonitor.App.Views;

/// <summary>
/// The single file allowed to touch the ScottPlot 5.x API. Listens to
/// <see cref="PowerChartViewModel.PlotDataChanged"/> and redraws the
/// <c>WpfPlot</c>; a live redraw happens at most once every 5s, a range switch
/// redraws immediately.
/// </summary>
public partial class PowerChartView : UserControl
{
    private static readonly TimeSpan MinRefreshInterval = TimeSpan.FromSeconds(5);

    private PowerChartViewModel? _vm;
    private long _lastRefreshTicks;

    public PowerChartView()
    {
        InitializeComponent();
        Plot.Plot.XLabel("Thời gian");
        Plot.Plot.YLabel("Công suất (W)");
        Plot.Plot.Axes.DateTimeTicksBottom();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => Detach();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();
        _vm = e.NewValue as PowerChartViewModel;
        if (_vm is null) return;
        _vm.PlotDataChanged += OnPlotDataChanged;
        Redraw(force: true);
    }

    private void Detach()
    {
        if (_vm is not null) _vm.PlotDataChanged -= OnPlotDataChanged;
        _vm = null;
    }

    private void OnPlotDataChanged(object? sender, bool isLiveAppend) => Redraw(force: !isLiveAppend);

    private void Redraw(bool force)
    {
        if (_vm is null) return;

        var now = DateTime.UtcNow.Ticks;
        if (!force && now - _lastRefreshTicks < MinRefreshInterval.Ticks) return;
        _lastRefreshTicks = now;

        double[] xs = _vm.Xs;
        double[] ys = _vm.Ys;

        // A ScottPlot exception here must never go unobserved — this handler runs off
        // an event, so an unhandled throw would otherwise either crash the app or (if
        // swallowed upstream) leave the chart frozen forever with no trace in the log.
        try
        {
            Plot.Plot.Clear();
            if (xs.Length > 1 && xs.Length == ys.Length)
            {
                var scatter = Plot.Plot.Add.Scatter(xs, ys);
                scatter.MarkerSize = 0;
                scatter.LineWidth = 1.6f;
            }

            Plot.Plot.Axes.DateTimeTicksBottom();
            Plot.Plot.Axes.AutoScale();
            Plot.Refresh();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Vẽ lại biểu đồ công suất lỗi (xsLen={XsLen}, ysLen={YsLen}).",
                xs.Length, ys.Length);
        }
    }
}
