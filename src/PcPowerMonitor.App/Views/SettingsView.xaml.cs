using System.Windows.Controls;

namespace PcPowerMonitor.App.Views;

/// <summary>
/// "Cài đặt" tab. DataContext is <c>SettingsViewModel</c> (via
/// <c>MainViewModel.Settings</c>). Pure XAML bindings — no code-behind logic needed
/// now that the Vertex AI service-account flow uses a file picker (no PasswordBox)
/// instead of the removed Gemini API-key text field.
/// </summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }
}
