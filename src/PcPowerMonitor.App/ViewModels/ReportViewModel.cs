using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.Core.Billing;
using PcPowerMonitor.Core.Billing.Reporting;

namespace PcPowerMonitor.App.ViewModels;

/// <summary>
/// "Báo cáo" tab. Owns the year picker, the 12-month table, the year totals and the
/// stale-tariff banner. All money maths lives in <see cref="IEnergyReportService"/> /
/// <see cref="ICsvExporter"/> (Core) — this class only orchestrates and formats.
/// Export / recalculate commands are in <c>ReportViewModel.Commands.cs</c>.
/// </summary>
public sealed partial class ReportViewModel : ObservableObject
{
    internal const string EstimateNote =
        "Số liệu là ƯỚC TÍNH công suất, không phải đo tại ổ cắm. " +
        "Tính theo đơn giá bậc 6 EVN (chi phí biên), không lũy tiến. " +
        "Tháng dương lịch, có thể lệch kỳ hóa đơn EVN.";

    private readonly IEnergyReportService _report;
    private readonly ICsvExporter _csv;
    private readonly ITariffProvider _tariff;
    private readonly ILogger<ReportViewModel>? _log;

    public ReportViewModel(
        IEnergyReportService report,
        ICsvExporter csv,
        ITariffProvider tariff,
        ILogger<ReportViewModel>? log = null)
    {
        _report = report ?? throw new ArgumentNullException(nameof(report));
        _csv = csv ?? throw new ArgumentNullException(nameof(csv));
        _tariff = tariff ?? throw new ArgumentNullException(nameof(tariff));
        _log = log;
        selectedYear = DateTimeOffset.Now.Year;
    }

    /// <summary>Note text for the UI. Constant; exposed as a property for binding.</summary>
    public string Note => EstimateNote;

    public ObservableCollection<int> AvailableYears { get; } = new();

    public ObservableCollection<MonthlyReportRow> Rows { get; } = new();

    [ObservableProperty] private int selectedYear;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? tariffWarning;
    [ObservableProperty] private bool hasTariffWarning;
    [ObservableProperty] private double totalKwh;
    [ObservableProperty] private double totalSubtotalVnd;
    [ObservableProperty] private double totalVatVnd;
    [ObservableProperty] private double totalCostVnd;
    [ObservableProperty] private double totalUptimeHours;

    /// <summary>Raised after <see cref="LoadAsync"/> refreshes <see cref="Rows"/>, so the
    /// bar chart can redraw. Handled on the UI thread by <c>ReportView</c>.</summary>
    public event EventHandler? ReportChanged;

    /// <summary>Set by the view: saves the chart PNG, returns an error message or null.</summary>
    public Func<string, string?>? ChartPngSaver { get; set; }

    partial void OnSelectedYearChanged(int value) => _ = LoadCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await RefreshAvailableYearsAsync().ConfigureAwait(true);

            var rows = await _report.GetMonthlyReportAsync(SelectedYear).ConfigureAwait(true);
            Rows.Clear();
            foreach (var row in rows) Rows.Add(row);
            UpdateTotals(rows);
            UpdateTariffWarning();

            ReportChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _log?.LogError(ex, "Tải báo cáo năm {Year} lỗi.", SelectedYear);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshAvailableYearsAsync()
    {
        var years = await _report.GetAvailableYearsAsync().ConfigureAwait(true);
        if (AvailableYears.SequenceEqual(years)) return;

        AvailableYears.Clear();
        foreach (var year in years) AvailableYears.Add(year);
        if (!AvailableYears.Contains(SelectedYear) && AvailableYears.Count > 0)
            SelectedYear = AvailableYears[0];
    }

    private void UpdateTotals(IReadOnlyList<MonthlyReportRow> rows)
    {
        TotalKwh = rows.Sum(r => r.Kwh);
        TotalSubtotalVnd = rows.Sum(r => r.SubtotalVnd);
        TotalVatVnd = rows.Sum(r => r.VatVnd);
        TotalCostVnd = rows.Sum(r => r.TotalVnd);
        TotalUptimeHours = rows.Sum(r => r.UptimeHours);
    }

    private void UpdateTariffWarning()
    {
        var freshness = TariffFreshnessChecker.Check(
            _tariff.Current, DateOnly.FromDateTime(DateTime.Now));
        HasTariffWarning = freshness.IsStale;
        TariffWarning = freshness.Message;
    }
}
