using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcPowerMonitor.Core.Sampling;

namespace PcPowerMonitor.App.Tray;

/// <summary>
/// Backs the tray context menu: Mở cửa sổ / Tạm dừng ghi / Cài đặt / Thoát.
/// "Tạm dừng ghi" toggles <see cref="SamplingOptions.Paused"/> and flips its own label.
/// </summary>
public sealed class TrayMenuViewModel : ObservableObject
{
    private const string PauseText = "Tạm dừng ghi";
    private const string ResumeText = "Tiếp tục ghi";

    private readonly SamplingOptions _sampling;
    private string _pauseLabel = PauseText;

    public TrayMenuViewModel(SamplingOptions sampling, Action showWindow, Action openSettings, Action exit)
    {
        _sampling = sampling ?? throw new ArgumentNullException(nameof(sampling));
        ArgumentNullException.ThrowIfNull(showWindow);
        ArgumentNullException.ThrowIfNull(openSettings);
        ArgumentNullException.ThrowIfNull(exit);

        ShowWindowCommand = new RelayCommand(showWindow);
        OpenSettingsCommand = new RelayCommand(openSettings);
        ExitCommand = new RelayCommand(exit);
        TogglePauseCommand = new RelayCommand(TogglePause);
        RefreshPauseLabel();
    }

    public ICommand ShowWindowCommand { get; }
    public ICommand TogglePauseCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }

    public string PauseLabel
    {
        get => _pauseLabel;
        private set => SetProperty(ref _pauseLabel, value);
    }

    private void TogglePause()
    {
        _sampling.Paused = !_sampling.Paused;
        RefreshPauseLabel();
    }

    private void RefreshPauseLabel() => PauseLabel = _sampling.Paused ? ResumeText : PauseText;
}
