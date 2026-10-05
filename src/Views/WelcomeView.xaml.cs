using System.Windows;
using System.Windows.Controls;
using Livy.Services;

namespace Livy.Views;

public partial class WelcomeView : UserControl
{
    public event Action? GetStartedClicked;

    public WelcomeView()
    {
        InitializeComponent();
    }

    private void GetStartedButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = SettingsService.Instance;
        settings.Settings.IsFirstRun = false;
        settings.Save();

        GetStartedClicked?.Invoke();
    }
}
