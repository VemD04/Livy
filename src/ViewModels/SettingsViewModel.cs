using Livy.Helpers;
using Livy.Services;

namespace Livy.ViewModels;

/// <summary>
/// ViewModel for the Settings page.
/// </summary>
public class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings = SettingsService.Instance;
    private readonly StartupService _startupService = new();
    private readonly WallpaperManager _wallpaperManager;

    // General
    private bool _confirmBeforeDelete;
    public bool ConfirmBeforeDelete
    {
        get => _confirmBeforeDelete;
        set
        {
            if (SetProperty(ref _confirmBeforeDelete, value))
            {
                _settings.Settings.ConfirmBeforeDelete = value;
                _settings.Save();
            }
        }
    }

    // Theme
    private string _theme = "Dark";
    public string Theme
    {
        get => _theme;
        set
        {
            if (SetProperty(ref _theme, value))
            {
                ThemeService.Instance.SetTheme(value);
                OnPropertyChanged(nameof(IsLightMode));
                OnPropertyChanged(nameof(ThemeLabel));
            }
        }
    }

    public bool IsLightMode
    {
        get => string.Equals(Theme, "Light", StringComparison.OrdinalIgnoreCase);
        set
        {
            Theme = value ? "Light" : "Dark";
            OnPropertyChanged();
            OnPropertyChanged(nameof(ThemeLabel));
        }
    }

    public string ThemeLabel => IsLightMode
        ? LocalizationService.GetString("Settings_ThemeLight", "Light Mode")
        : LocalizationService.GetString("Settings_ThemeDark", "Dark Mode");

    // Language
    private string _language = "English";
    public string Language
    {
        get => _language;
        set
        {
            if (SetProperty(ref _language, value))
            {
                LocalizationService.Instance.SetLanguage(value);
            }
        }
    }

    // Startup
    private bool _startWithWindows;
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (SetProperty(ref _startWithWindows, value))
            {
                _startupService.SetStartWithWindows(value);
                _settings.Settings.StartWithWindows = value;
                _settings.Save();
            }
        }
    }

    // Audio
    private bool _audioEnabled;
    public bool AudioEnabled
    {
        get => _audioEnabled;
        set
        {
            if (SetProperty(ref _audioEnabled, value))
            {
                _settings.Settings.AudioEnabled = value;
                _settings.Save();
                _wallpaperManager.SetMute(!value);
            }
        }
    }

    private int _volume;
    public int Volume
    {
        get => _volume;
        set
        {
            if (SetProperty(ref _volume, value))
            {
                _settings.Settings.Volume = value;
                _settings.Save();
                _wallpaperManager.SetVolume(value);
            }
        }
    }

    // Wallpaper
    private string _wallpaperScaling = "Fill";
    public string WallpaperScaling
    {
        get => _wallpaperScaling;
        set
        {
            if (SetProperty(ref _wallpaperScaling, value))
            {
                _settings.Settings.WallpaperScaling = value;
                _settings.Save();
                if (_wallpaperManager.IsRunning)
                {
                    // Use SetScaling() for a live, no-flicker scaling update
                    _wallpaperManager.SetScaling(value);
                }
                OnPropertyChanged(nameof(ScalingStretchMode));
                OnPropertyChanged(nameof(ScalingDescription));
            }
        }

    }

    private string _wallpaperDisplay = "All";
    public string WallpaperDisplay
    {
        get => _wallpaperDisplay;
        set
        {
            if (SetProperty(ref _wallpaperDisplay, value))
            {
                _settings.Settings.WallpaperDisplay = value;
                _settings.Save();
                if (_wallpaperManager.IsRunning)
                {
                    _wallpaperManager.RefreshWallpaper();
                }
                OnPropertyChanged(nameof(DisplayDescription));
            }
        }
    }

    // Wallpaper Preview Properties
    private readonly MonitorService _monitorService = new();

    public System.Windows.Media.ImageSource? ActiveWallpaperThumbnail
    {
        get
        {
            var activeId = _settings.Settings.ActiveWallpaperId;
            if (activeId is not null)
            {
                var wp = _settings.Settings.Wallpapers.FirstOrDefault(w => w.Id == activeId);
                if (wp is not null)
                {
                    if (wp.Thumbnail is null && wp.FileExists)
                    {
                        wp.LoadThumbnailAsync();
                    }
                    return wp.Thumbnail;
                }
            }
            return null;
        }
    }

    public string ActiveWallpaperName
    {
        get
        {
            var activeId = _settings.Settings.ActiveWallpaperId;
            if (activeId is not null)
            {
                var wp = _settings.Settings.Wallpapers.FirstOrDefault(w => w.Id == activeId);
                if (wp is not null)
                {
                    return wp.FileName;
                }
            }
            return LocalizationService.GetString("Settings_SampleWallpaper", "Sample Wallpaper");
        }
    }

    public bool HasActiveWallpaper
    {
        get
        {
            var activeId = _settings.Settings.ActiveWallpaperId;
            return activeId is not null && ActiveWallpaperThumbnail is not null;
        }
    }

    public System.Windows.Media.Stretch ScalingStretchMode => WallpaperScaling switch
    {
        "Fit" => System.Windows.Media.Stretch.Uniform,
        "Stretch" => System.Windows.Media.Stretch.Fill,
        "Center" => System.Windows.Media.Stretch.None,
        _ => System.Windows.Media.Stretch.UniformToFill
    };

    public string ScalingDescription => WallpaperScaling switch
    {
        "Fit" => LocalizationService.GetString("Settings_DescFit", "Entire wallpaper is visible without cropping; black bars may appear on sides."),
        "Stretch" => LocalizationService.GetString("Settings_DescStretch", "Wallpaper is stretched to fill the entire screen, ignoring aspect ratio."),
        "Center" => LocalizationService.GetString("Settings_DescCenter", "Wallpaper is centered at its original resolution without scaling."),
        _ => LocalizationService.GetString("Settings_DescFill", "Wallpaper fills the entire screen, cropping edges if aspect ratios differ.")
    };

    public string DisplayDescription
    {
        get
        {
            var monitors = _monitorService.GetMonitors();
            var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
            var primaryRes = primary != null ? $"{primary.Width}×{primary.Height}" : "";

            return WallpaperDisplay switch
            {
                "Primary" => string.Format(LocalizationService.GetString("Settings_DescPrimaryOnly", "Active on Primary Monitor only ({0})"), primaryRes),
                _ => string.Format(LocalizationService.GetString("Settings_DescAllMonitors", "Active across all connected displays ({0} displays)"), monitors.Count)
            };
        }
    }

    public SettingsViewModel(WallpaperManager wallpaperManager)
    {
        _wallpaperManager = wallpaperManager;
        _wallpaperManager.StateChanged += Refresh;
        LocalizationService.Instance.LanguageChanged += Refresh;

        Refresh();
    }

    public void Refresh()
    {
        var s = _settings.Settings;
        _confirmBeforeDelete = s.ConfirmBeforeDelete;
        _theme = s.Theme;
        _language = s.Language;
        _startWithWindows = _startupService.IsStartWithWindowsEnabled();
        _audioEnabled = s.AudioEnabled;
        _volume = s.Volume;
        _wallpaperScaling = s.WallpaperScaling;
        _wallpaperDisplay = s.WallpaperDisplay;

        // Notify all properties
        OnPropertyChanged(nameof(ConfirmBeforeDelete));
        OnPropertyChanged(nameof(Theme));
        OnPropertyChanged(nameof(IsLightMode));
        OnPropertyChanged(nameof(ThemeLabel));
        OnPropertyChanged(nameof(Language));
        OnPropertyChanged(nameof(StartWithWindows));
        OnPropertyChanged(nameof(AudioEnabled));
        OnPropertyChanged(nameof(Volume));
        OnPropertyChanged(nameof(WallpaperScaling));
        OnPropertyChanged(nameof(WallpaperDisplay));
        OnPropertyChanged(nameof(ActiveWallpaperThumbnail));
        OnPropertyChanged(nameof(ActiveWallpaperName));
        OnPropertyChanged(nameof(HasActiveWallpaper));
        OnPropertyChanged(nameof(ScalingStretchMode));
        OnPropertyChanged(nameof(ScalingDescription));
        OnPropertyChanged(nameof(DisplayDescription));
    }
}
