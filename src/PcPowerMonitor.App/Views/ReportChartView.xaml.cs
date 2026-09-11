using System.IO;
using System.Linq;
using System.Windows.Controls;

namespace PcPowerMonitor.App.Views;

/// <summary>
/// kWh-by-month bar chart for the report tab. The only report file that touches the
/// ScottPlot 5.0.47 API: <c>Plot.Add.Bars</c> + <c>NumericManual</c> tick labels, and
/// <c>Plot.SavePng</c> for the export button.
/// </summary>
public partial class ReportChartView : UserControl
{
    private const int PngWidth = 1000;
    private const int PngHeight = 500;

    public ReportChartView()
    {
        InitializeComponent();
        Plot.Plot.XLabel("Tháng");
        Plot.Plot.YLabel("kWh");
        Plot.Plot.HideLegend();
    }

    /// <summary>Redraw the bars. <paramref name="labels"/> aligns with <paramref name="values"/>.</summary>
    public void Render(IReadOnlyList<string> labels, IReadOnlyList<double> values)
    {
        Plot.Plot.Clear();

        if (values.Count > 0)
        {
            var positions = Enumerable.Range(1, values.Count).Select(i => (double)i).ToArray();
            Plot.Plot.Add.Bars(positions, values);

            var tickLabels = positions
                .Select((p, i) => i < labels.Count ? labels[i] : p.ToString("0"))
                .ToArray();
            Plot.Plot.Axes.Bottom.TickGenerator =
                new ScottPlot.TickGenerators.NumericManual(positions, tickLabels);
            Plot.Plot.Axes.Margins(bottom: 0);
        }

        Plot.Plot.Axes.AutoScale();
        Plot.Refresh();
    }

    /// <summary>Save the current chart as PNG. Returns null on success, else an error message.</summary>
    public string? TrySavePng(string path)
    {
        try
        {
            Plot.Plot.SavePng(path, PngWidth, PngHeight);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return ex.Message;
        }
    }
}
