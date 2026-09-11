using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PcPowerMonitor.Core.Models;
using PcPowerMonitor.Core.Ui;

namespace PcPowerMonitor.App.ViewModels;

/// <summary>
/// "Phần cứng" tab. Applies a snapshot by updating the <see cref="SensorItemViewModel.Value"/>
/// of pre-existing rows (looked up in <see cref="_index"/>) instead of clearing and
/// rebuilding the collections. New keys are appended only when hardware topology
/// changes (rare). Callers marshal this onto the UI thread.
/// </summary>
public sealed partial class HardwareViewModel : ObservableObject
{
    private readonly Dictionary<string, SensorItemViewModel> _index = new();
    private readonly Dictionary<string, SensorGroupViewModel> _groups = new();

    public ObservableCollection<SensorGroupViewModel> Groups { get; } = new();

    /// <summary>False while no sensor rows exist — the view shows <see cref="EmptyMessage"/>.</summary>
    [ObservableProperty]
    private bool hasData;

    [ObservableProperty]
    private string emptyMessage =
        "Chưa đọc được cảm biến phần cứng. Chạy ứng dụng bằng quyền Administrator " +
        "hoặc kiểm tra Windows Defender / phần mềm diệt virus.";

    public void ApplySnapshot(HardwareSnapshot snapshot)
    {
        var rows = HardwareSnapshotToRowsMapper.Map(snapshot);

        foreach (var row in rows)
        {
            var key = row.Group + "|" + row.Name;
            if (_index.TryGetValue(key, out var existing))
            {
                existing.Value = row.Value;
                continue;
            }

            var group = GetOrAddGroup(row.Group);
            var item = new SensorItemViewModel(row.Group, row.Name, row.Unit, row.Value);
            group.Items.Add(item);
            _index[key] = item;
        }

        HasData = _index.Count > 0;
    }

    private SensorGroupViewModel GetOrAddGroup(string name)
    {
        if (_groups.TryGetValue(name, out var group)) return group;
        group = new SensorGroupViewModel(name);
        _groups[name] = group;
        Groups.Add(group);
        return group;
    }
}
