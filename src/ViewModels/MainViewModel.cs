using Livy.Helpers;
using Livy.Services;

namespace Livy.ViewModels;

/// <summary>
/// Main ViewModel managing navigation between views.
/// </summary>
public class MainViewModel : ObservableObject
{
    private ObservableObject? _currentView;
    private string _currentPage = "Home";

    public ObservableObject? CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public string CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    public HomeViewModel HomeVM { get; }
    public SettingsViewModel SettingsVM { get; }
    public AboutViewModel AboutVM { get; }

    public RelayCommand NavigateHomeCommand { get; }
    public RelayCommand NavigateSettingsCommand { get; }
    public RelayCommand NavigateAboutCommand { get; }

    public WallpaperManager WallpaperManager { get; }

    public MainViewModel()
    {
        WallpaperManager = new WallpaperManager();

        HomeVM = new HomeViewModel(WallpaperManager);
        SettingsVM = new SettingsViewModel(WallpaperManager);
        AboutVM = new AboutViewModel();

        NavigateHomeCommand = new RelayCommand(() => NavigateTo("Home"));
        NavigateSettingsCommand = new RelayCommand(() => NavigateTo("Settings"));
        NavigateAboutCommand = new RelayCommand(() => NavigateTo("About"));

        // Start on home
        NavigateTo("Home");
    }

    private void NavigateTo(string page)
    {
        CurrentPage = page;
        CurrentView = page switch
        {
            "Home" => HomeVM,
            "Settings" => SettingsVM,
            "About" => AboutVM,
            _ => HomeVM
        };

        // Refresh data when navigating
        if (page == "Home") HomeVM.Refresh();
        if (page == "Settings") SettingsVM.Refresh();
    }
}
