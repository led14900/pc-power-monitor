using System.ComponentModel;
using System.Net.Http;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PcPowerMonitor.App.Views;
using PcPowerMonitor.Core.Ai;

namespace PcPowerMonitor.App.ViewModels;

/// <summary>
/// "Dò thông số bằng AI" orchestration: read the live hardware snapshot, build the
/// Vertex AI <see cref="IAiClient"/> via <see cref="AiClientFactory"/>, build the
/// prompt, call the API, parse the response, show the diff confirmation dialog, and
/// load the suggested profile ONLY if the user clicks "Áp dụng". All failure modes
/// surface as a Vietnamese <see cref="ErrorMessage"/> instead of a MessageBox,
/// matching the lightweight status-message convention used elsewhere on this screen
/// (see <c>SettingsViewModel.StatusMessage</c>).
/// </summary>
public sealed partial class HardwareProfileSectionViewModel
{
    private static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(30);

    [ObservableProperty] private bool isLookupEnabled;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LookupWithAiCommand))]
    private bool isLookupBusy;

    [ObservableProperty] private string? errorMessage;

    private void OnAiSectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AiSectionViewModel.HasValidServiceAccount)
                            or nameof(AiSectionViewModel.ProjectId))
            RefreshLookupAvailability();
    }

    /// <summary>
    /// Recomputes button enablement for the Vertex AI lookup. Called at startup and
    /// whenever the stored service account/Project ID changes.
    /// </summary>
    public void RefreshLookupAvailability()
    {
        IsLookupEnabled = _aiSection.HasValidServiceAccount && !string.IsNullOrWhiteSpace(_aiSection.ProjectId);
        LookupWithAiCommand.NotifyCanExecuteChanged();
    }

    private bool CanLookup() => !IsLookupBusy && IsLookupEnabled;

    [RelayCommand(CanExecute = nameof(CanLookup))]
    private async Task LookupWithAiAsync()
    {
        IsLookupBusy = true;
        ErrorMessage = null;
        try
        {
            await DoLookupAsync();
        }
        finally
        {
            IsLookupBusy = false;
        }
    }

    private async Task DoLookupAsync()
    {
        // LHM sensor reads hit a kernel driver / WMI — keep them off the UI thread.
        var snapshot = await Task.Run(() => _sensorReader.ReadSnapshot());

        var aiSettings = _store.Current.Ai;
        // `using`: the real client owns a native RSA/CNG key handle (JWT signing) that
        // must be released after this one lookup — it's re-created fresh from
        // AiClientFactory on every click, not cached across calls.
        using var client = AiClientFactory.TryCreate(aiSettings);
        if (client == null)
        {
            ErrorMessage = "Cấu hình Vertex AI không hợp lệ. Kiểm tra file service account và Project ID.";
            return;
        }

        var prompt = PromptBuilder.Build(snapshot, _aiSection.MachineModel, _aiSection.DeviceType);
        string response;
        try
        {
            using var cts = new CancellationTokenSource(LookupTimeout);
            response = await client.GenerateAsync(prompt, cts.Token);
        }
        catch (AiClientException ex)
        {
            _log?.LogWarning(ex, "Tra cứu AI: API trả lỗi.");
            ErrorMessage = ex.Message;
            return;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Tra cứu quá thời gian (30 giây). Vui lòng thử lại.";
            return;
        }
        catch (HttpRequestException ex)
        {
            _log?.LogWarning(ex, "Tra cứu AI: lỗi kết nối mạng.");
            ErrorMessage = "Không thể kết nối tới máy chủ AI. Kiểm tra mạng.";
            return;
        }

        var suggestion = ResponseParser.Parse(response);
        var diffVm = new ProfileDiffViewModel(ToProfile(), suggestion);
        var dialog = new ProfileSuggestionDialog(diffVm) { Owner = Application.Current?.MainWindow };

        if (dialog.ShowDialog() == true && dialog.Applied)
        {
            Load(suggestion.Profile);
        }
    }
}
