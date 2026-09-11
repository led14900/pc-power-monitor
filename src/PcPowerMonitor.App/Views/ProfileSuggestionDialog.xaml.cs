using System.Windows;
using PcPowerMonitor.App.ViewModels;

namespace PcPowerMonitor.App.Views;

/// <summary>
/// Modal AI-lookup confirmation dialog. <see cref="Applied"/> is only ever set true by
/// an explicit click on "Áp dụng" — closing via the X button or "Hủy" leaves the
/// caller's hardware profile fields untouched.
/// </summary>
public partial class ProfileSuggestionDialog : Window
{
    public bool Applied { get; private set; }

    public ProfileSuggestionDialog(ProfileDiffViewModel vm)
    {
        InitializeComponent();
        DataContext = vm ?? throw new ArgumentNullException(nameof(vm));
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        Applied = true;
        DialogResult = true;
    }
}
