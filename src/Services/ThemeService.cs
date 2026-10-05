using System.Windows;

namespace Livy.Services;

/// <summary>
/// Manages dynamic theme switching between Dark and Light mode by swapping XAML ResourceDictionaries at runtime.
/// </summary>
public class ThemeService
{
    private static readonly Lazy<ThemeService> _instance = new(() => new ThemeService());
    public static ThemeService Instance => _instance.Value;

    private readonly SettingsService _settings = SettingsService.Instance;
    private readonly LogService _log = LogService.Instance;

    public const string Dark = "Dark";
    public const string Light = "Light";

    public event Action? ThemeChanged;

    public string CurrentTheme => _settings.Settings.Theme;

    public void Initialize()
    {
        var theme = NormalizeTheme(_settings.Settings.Theme);
        ApplyThemeDictionary(theme, saveSettings: false);
    }

    public void SetTheme(string theme)
    {
        var normalized = NormalizeTheme(theme);
        ApplyThemeDictionary(normalized, saveSettings: true);
    }

    private static string NormalizeTheme(string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
        {
            return Dark;
        }

        if (theme.Equals(Light, StringComparison.OrdinalIgnoreCase))
        {
            return Light;
        }

        return Dark;
    }

    private void ApplyThemeDictionary(string theme, bool saveSettings)
    {
        try
        {
            var dictFile = theme == Light ? "Colors.Light.xaml" : "Colors.Dark.xaml";
            var uri = new Uri($"pack://application:,,,/Livy;component/Resources/{dictFile}", UriKind.RelativeOrAbsolute);
            var newDict = new ResourceDictionary { Source = uri };

            var merged = Application.Current.Resources.MergedDictionaries;
            ResourceDictionary? existing = null;

            foreach (var d in merged)
            {
                if (d.Source != null && (d.Source.OriginalString.Contains("Colors.") || d.Source.OriginalString.EndsWith("Colors.xaml")))
                {
                    existing = d;
                    break;
                }
            }

            if (existing != null)
            {
                var index = merged.IndexOf(existing);
                merged.RemoveAt(index);
                merged.Insert(index, newDict);
            }
            else
            {
                merged.Insert(0, newDict);
            }

            if (saveSettings)
            {
                _settings.Settings.Theme = theme;
                _settings.Save();
            }

            _log.Info($"Theme changed to: {theme}");
            ThemeChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to switch theme to: {theme}", ex);
        }
    }
}
