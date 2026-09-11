using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.Core.Storage;
using PcPowerMonitor.Core.Storage.Dtos;

namespace PcPowerMonitor.App.ViewModels;

/// <summary>
/// Owns the power-over-time series and the selected <see cref="ChartRange"/>. Queries
/// the DB off the UI thread, inserts <see cref="double.NaN"/> where samples are more
/// than 2x the bucket apart (so the line breaks over sleep) and raises
/// <see cref="PlotDataChanged"/> for the view to redraw. Holds no ScottPlot types.
/// </summary>
public sealed partial class PowerChartViewModel : ObservableObject
{
    private const int LastHourBucketSeconds = 2;
    private const int Last24hBucketSeconds = 60;
    private const int Last7dBucketSeconds = 3600;
    private const int LiveWindowPoints = 1800;

    private readonly IEnergyQueryService _query;
    private readonly ILogger<PowerChartViewModel>? _log;
    private readonly List<double> _liveX = new(LiveWindowPoints + 1);
    private readonly List<double> _liveY = new(LiveWindowPoints + 1);

    public PowerChartViewModel(IEnergyQueryService query, ILogger<PowerChartViewModel>? log = null)
    {
        _query = query;
        _log = log;
    }

    /// <summary>OADate X values (local time). Read by the view on <see cref="PlotDataChanged"/>.</summary>
    public double[] Xs { get; private set; } = Array.Empty<double>();

    /// <summary>Watt Y values, aligned with <see cref="Xs"/>; NaN marks a gap.</summary>
    public double[] Ys { get; private set; } = Array.Empty<double>();

    /// <summary>Raised after the series changes. The flag is true for a live append
    /// (view should honour its 5s redraw throttle), false for a full reload (redraw now).</summary>
    public event EventHandler<bool>? PlotDataChanged;

    [ObservableProperty] private ChartRange range = ChartRange.LastHour;
    [ObservableProperty] private bool isLoading;

    [RelayCommand]
    private async Task SetRangeAsync(ChartRange value)
    {
        Range = value;
        await LoadAsync().ConfigureAwait(true);
    }

    /// <summary>Query the DB for the current range and publish a fresh series. Called
    /// fire-and-forget from <c>MainViewModel.Initialize()</c>/<c>Resume()</c> and from
    /// <see cref="SetRangeAsync"/>, so a failure here must never go unobserved — a bug
    /// that silently swallowed it here used to leave the chart permanently blank with
    /// no trace in the log.</summary>
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var now = DateTimeOffset.Now;
            var (from, bucket) = Range switch
            {
                ChartRange.Last24Hours => (now.AddHours(-24), Last24hBucketSeconds),
                ChartRange.Last7Days => (now.AddDays(-7), Last7dBucketSeconds),
                _ => (now.AddHours(-1), LastHourBucketSeconds),
            };

            var points = await Task.Run(() => _query.GetPowerSeries(from, now, bucket))
                .ConfigureAwait(true);

            BuildArrays(points, bucket);

            if (Range == ChartRange.LastHour) SeedLiveWindow();
            else { _liveX.Clear(); _liveY.Clear(); }

            PlotDataChanged?.Invoke(this, false);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Tải dữ liệu biểu đồ công suất lỗi (range={Range}).", Range);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Append one live point; only used while <see cref="ChartRange.LastHour"/>.</summary>
    public void AppendLatest(DateTimeOffset timestamp, double watt)
    {
        if (Range != ChartRange.LastHour) return;

        _liveX.Add(timestamp.LocalDateTime.ToOADate());
        _liveY.Add(watt);
        if (_liveX.Count > LiveWindowPoints)
        {
            _liveX.RemoveAt(0);
            _liveY.RemoveAt(0);
        }

        Xs = _liveX.ToArray();
        Ys = _liveY.ToArray();
        PlotDataChanged?.Invoke(this, true);
    }

    private void BuildArrays(IReadOnlyList<PowerPoint> points, int bucketSeconds)
    {
        var xs = new List<double>(points.Count + 8);
        var ys = new List<double>(points.Count + 8);
        var gapThreshold = TimeSpan.FromSeconds(bucketSeconds * 2);

        for (var i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (i > 0)
            {
                var prev = points[i - 1].Timestamp;
                if (p.Timestamp - prev > gapThreshold)
                {
                    var mid = prev + TimeSpan.FromTicks((p.Timestamp - prev).Ticks / 2);
                    xs.Add(mid.LocalDateTime.ToOADate());
                    ys.Add(double.NaN);
                }
            }

            xs.Add(p.Timestamp.LocalDateTime.ToOADate());
            ys.Add(p.AvgW);
        }

        Xs = xs.ToArray();
        Ys = ys.ToArray();
    }

    private void SeedLiveWindow()
    {
        _liveX.Clear();
        _liveY.Clear();
        var start = Math.Max(0, Xs.Length - LiveWindowPoints);
        for (var i = start; i < Xs.Length; i++)
        {
            _liveX.Add(Xs[i]);
            _liveY.Add(Ys[i]);
        }
    }
}
