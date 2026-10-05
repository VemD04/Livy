using System.Windows;

namespace Livy.Services;

/// <summary>
/// Manages dynamic localization between English and Indonesian by swapping XAML ResourceDictionaries at runtime.
/// </summary>
public class LocalizationService
{
    private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
    public static LocalizationService Instance => _instance.Value;

    private readonly SettingsService _settings = SettingsService.Instance;
    private readonly LogService _log = LogService.Instance;

    public const string English = "English";
    public const string Indonesian = "Indonesian";

    public event Action? LanguageChanged;

    public string CurrentLanguage => _settings.Settings.Language;

    public void Initialize()
    {
        var lang = NormalizeLanguage(_settings.Settings.Language);
        ApplyLanguageDictionary(lang, saveSettings: false);
    }

    public void SetLanguage(string language)
    {
        var normalized = NormalizeLanguage(language);
        ApplyLanguageDictionary(normalized, saveSettings: true);
    }

    private static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return English;
        }

        if (language.Equals("Bahasa Indonesia", StringComparison.OrdinalIgnoreCase) ||
            language.Equals("Indonesian", StringComparison.OrdinalIgnoreCase) ||
            language.Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            return Indonesian;
        }

        return English;
    }

    private void ApplyLanguageDictionary(string language, bool saveSettings)
    {
        try
        {
            var dictFile = language == Indonesian ? "Strings.id.xaml" : "Strings.en.xaml";
            var uri = new Uri($"pack://application:,,,/Livy;component/Resources/{dictFile}", UriKind.RelativeOrAbsolute);
            var newDict = new ResourceDictionary { Source = uri };

            var merged = Application.Current.Resources.MergedDictionaries;
            ResourceDictionary? existing = null;

            foreach (var d in merged)
            {
                if (d.Source != null && d.Source.OriginalString.Contains("Strings."))
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
                merged.Add(newDict);
            }

            if (saveSettings)
            {
                _settings.Settings.Language = language;
                _settings.Save();
            }

            _log.Info($"Language changed to: {language}");
            LanguageChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to switch language to: {language}", ex);
        }
    }

    public static string GetString(string key, string fallback = "")
    {
        try
        {
            if (Application.Current.TryFindResource(key) is string s)
            {
                return s;
            }
        }
        catch
        {
            // Ignore resource lookup errors
        }

        return fallback.Length > 0 ? fallback : key;
    }
}
