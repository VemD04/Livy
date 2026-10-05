using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Livy.Helpers;
using Livy.Services;
using Livy.ViewModels;

namespace Livy.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var mainViewModel = new MainViewModel();
        DataContext = mainViewModel;

        var settings = SettingsService.Instance;

        // Show welcome screen on first run
        if (settings.Settings.IsFirstRun)
        {
            WelcomeOverlay.Visibility = Visibility.Visible;
            WelcomeOverlay.GetStartedClicked += () =>
            {
                WelcomeOverlay.Visibility = Visibility.Collapsed;
            };
        }

        // Apply active wallpaper if it exists
        if (!settings.Settings.IsFirstRun && !string.IsNullOrEmpty(settings.Settings.ActiveWallpaperId))
        {
            var wallpaper = settings.Settings.Wallpapers.FirstOrDefault(w => w.Id == settings.Settings.ActiveWallpaperId);
            if (wallpaper is not null && wallpaper.FileExists)
            {
                mainViewModel.WallpaperManager.ApplyWallpaper(wallpaper.FilePath, wallpaper.LoopMode);
            }
        }
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        // Apply Windows 11 native rounded corners via DWM.
        // Key: AllowsTransparency was removed — layered windows block the DWM corner API.
        // Without AllowsTransparency, DWM clips all 4 corners consistently at the GPU compositor level.
        NativeMethods.ApplyRoundedCorners(hwnd, NativeMethods.DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND);
        NativeMethods.HideDwmBorder(hwnd);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            Maximize_Click(sender, e);
        }
        else
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        // Hide window instead of closing (goes to system tray)
        Hide();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            // When maximized, content should fill edge-to-edge (no rounded corners).
            // DWM also automatically removes rounding on maximized windows on Windows 11.
            WindowBorder.CornerRadius = new CornerRadius(0);
        }
        else
        {
            WindowBorder.CornerRadius = new CornerRadius(14);
        }
    }
}
