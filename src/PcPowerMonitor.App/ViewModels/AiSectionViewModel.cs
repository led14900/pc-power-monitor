using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PcPowerMonitor.Core.Security;
using PcPowerMonitor.Core.Settings;

namespace PcPowerMonitor.App.ViewModels;

/// <summary>
/// "Cấu hình AI" group of the Settings screen. Registered as a DI singleton
/// (see <c>HostBuilderExtensions</c>) because both <see cref="SettingsViewModel"/> and
/// <see cref="HardwareProfileSectionViewModel"/> need the SAME instance — the lookup
/// flow reads <see cref="MachineModel"/>/<see cref="ModelId"/>/<see cref="DeviceType"/>
/// and reacts to <see cref="HasValidServiceAccount"/> changes live as the user edits
/// them here. Vertex AI is the only AI provider (see project scope decision — no more
/// Gemini API-key path/provider selector). <see cref="ServiceAccountEmail"/> is
/// display-only, parsed purely for user confirmation that the right file was picked —
/// the <c>private_key</c> field is never read into a bindable property or shown anywhere.
/// <see cref="ProjectId"/> is auto-filled from the picked file's <c>project_id</c> field
/// (still editable, in case the user needs to override it).
/// </summary>
public sealed partial class AiSectionViewModel : ObservableObject
{
    private readonly ISettingsStore _store;

    // Staged (not-yet-persisted) encrypted service-account blob, mirroring how every
    // other field on this screen behaves: edits here only reach the store when the user
    // clicks "Lưu" (see SettingsViewModel.Commands.Save -> BuildSettings -> ToSettings).
    // PickServiceAccount/ClearServiceAccount used to call _store.Save() directly, which
    // bypassed that gate — fixed so a pick/clear the user never saves is discarded on
    // the next Load() (e.g. navigating away and back, or closing the window).
    private string? _pendingEncryptedServiceAccountJson;

    [ObservableProperty] private string modelId = "gemini-3.5-flash-lite";
    [ObservableProperty] private string machineModel = string.Empty;
    [ObservableProperty] private AiDeviceType deviceType = AiDeviceType.Desktop;

    [ObservableProperty] private string serviceAccountEmail = string.Empty;
    [ObservableProperty] private bool hasValidServiceAccount;
    [ObservableProperty] private string projectId = string.Empty;
    [ObservableProperty] private string region = "us-central1";

    /// <summary>
    /// True when <see cref="ServiceAccountEmail"/> actually holds an error message
    /// (bad file, malformed JSON) rather than a confirmed client_email — drives the
    /// red error text in <c>SettingsView.xaml</c> separately from the green checkmark.
    /// </summary>
    public bool HasServiceAccountError => !HasValidServiceAccount && !string.IsNullOrEmpty(ServiceAccountEmail);

    partial void OnServiceAccountEmailChanged(string value) => OnPropertyChanged(nameof(HasServiceAccountError));

    partial void OnHasValidServiceAccountChanged(bool value) => OnPropertyChanged(nameof(HasServiceAccountError));

    public AiSectionViewModel(ISettingsStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        Load(_store.Current.Ai);
    }

    /// <summary>Reloads editable fields from persisted settings. Never loads the service-account secret itself.</summary>
    public void Load(AiSettings ai)
    {
        ArgumentNullException.ThrowIfNull(ai);
        ModelId = string.IsNullOrWhiteSpace(ai.ModelId) ? "gemini-3.5-flash-lite" : ai.ModelId;
        MachineModel = ai.MachineModel ?? string.Empty;
        DeviceType = ai.DeviceType;
        ProjectId = ai.ProjectId ?? string.Empty;
        Region = string.IsNullOrWhiteSpace(ai.Region) ? "us-central1" : ai.Region;
        _pendingEncryptedServiceAccountJson = ai.EncryptedServiceAccountJson;
        RefreshServiceAccountStatus(_pendingEncryptedServiceAccountJson);
    }

    [RelayCommand]
    private void PickServiceAccount()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Chọn file Service Account JSON",
            Filter = "JSON files (*.json)|*.json",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() != true) return;

        string json;
        try
        {
            json = File.ReadAllText(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ServiceAccountEmail = $"Lỗi đọc file: {ex.Message}";
            HasValidServiceAccount = false;
            return;
        }

        // Validate just enough to confirm this looks like a service-account key
        // (client_email + private_key present) without ever exposing the key value.
        // Also pull project_id straight from the file — every service-account key is
        // already scoped to exactly one GCP project, so typing it by hand is both
        // redundant and a source of mismatch bugs (key from project A, Project ID field
        // still says project B). Overwrite unconditionally: the freshly-picked file is
        // authoritative over whatever was typed before.
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("client_email", out _) ||
                !doc.RootElement.TryGetProperty("private_key", out _))
            {
                ServiceAccountEmail = "File không hợp lệ (thiếu client_email hoặc private_key)";
                HasValidServiceAccount = false;
                return;
            }

            if (doc.RootElement.TryGetProperty("project_id", out var projectIdProp))
            {
                var projectIdValue = projectIdProp.GetString();
                if (!string.IsNullOrWhiteSpace(projectIdValue))
                    ProjectId = projectIdValue;
            }
        }
        catch (JsonException)
        {
            ServiceAccountEmail = "File JSON không hợp lệ";
            HasValidServiceAccount = false;
            return;
        }

        // Stage only — persisted when the user clicks "Lưu" (ToSettings() below), same
        // as every other field on this screen. Not written to the store here.
        _pendingEncryptedServiceAccountJson = DpapiKeyProtector.Encrypt(json);
        RefreshServiceAccountStatus(_pendingEncryptedServiceAccountJson);
    }

    [RelayCommand]
    private void ClearServiceAccount()
    {
        _pendingEncryptedServiceAccountJson = null;
        ServiceAccountEmail = string.Empty;
        HasValidServiceAccount = false;
    }

    private void RefreshServiceAccountStatus(string? encrypted)
    {
        if (string.IsNullOrEmpty(encrypted) || !DpapiKeyProtector.TryDecrypt(encrypted, out var json))
        {
            ServiceAccountEmail = string.Empty;
            HasValidServiceAccount = false;
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            ServiceAccountEmail = doc.RootElement.TryGetProperty("client_email", out var emailProp)
                ? emailProp.GetString() ?? "???"
                : "???";
            HasValidServiceAccount = true;
        }
        catch (JsonException)
        {
            ServiceAccountEmail = "JSON không hợp lệ";
            HasValidServiceAccount = false;
        }
    }

    /// <summary>
    /// Builds the persisted <see cref="AiSettings"/> record, including whatever
    /// <see cref="PickServiceAccountCommand"/>/<see cref="ClearServiceAccountCommand"/>
    /// staged in <see cref="_pendingEncryptedServiceAccountJson"/> — only committed to
    /// disk when the caller (<c>SettingsViewModel.Save</c>) actually persists this.
    /// </summary>
    public AiSettings ToSettings() => new()
    {
        EncryptedServiceAccountJson = _pendingEncryptedServiceAccountJson,
        ModelId = string.IsNullOrWhiteSpace(ModelId) ? "gemini-3.5-flash-lite" : ModelId,
        MachineModel = string.IsNullOrWhiteSpace(MachineModel) ? null : MachineModel,
        DeviceType = DeviceType,
        ProjectId = string.IsNullOrWhiteSpace(ProjectId) ? null : ProjectId,
        Region = string.IsNullOrWhiteSpace(Region) ? "us-central1" : Region,
    };
}
