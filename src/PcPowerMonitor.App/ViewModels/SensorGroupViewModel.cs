using System.Collections.ObjectModel;

namespace PcPowerMonitor.App.ViewModels;

/// <summary>A named block of sensor rows (CPU / GPU / RAM / …) in the hardware tab.</summary>
public sealed class SensorGroupViewModel
{
    public SensorGroupViewModel(string name) => Name = name;

    public string Name { get; }

    public ObservableCollection<SensorItemViewModel> Items { get; } = new();
}
