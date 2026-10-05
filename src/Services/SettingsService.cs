using System.IO;
using Livy.Models;
using Newtonsoft.Json;

namespace Livy.Services;

/// <summary>
/// Loads, saves, and manages application settings stored as JSON in %AppData%\Livy.
/// </summary>
public class SettingsService
{
    private static readonly Lazy<SettingsService> _instance = new(() => new SettingsService());
    public static SettingsService Instance => _instance.Value;

    private readonly string _settingsDir;
    private readonly string _settingsFile;
    private readonly LogService _log = LogService.Instance;

    public AppSettings Settings { get; private set; } = new();

    private SettingsService()
    {
        _settingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Livy");
        _settingsFile = Path.Combine(_settingsDir, "settings.json");
        Directory.CreateDirectory(_settingsDir);
    }

    /// <summary>
    /// Loads settings from disk. Creates default settings if file doesn't exist.
    /// </summary>
    public void Load()
    {
        try
        {
            if (File.Exists(_settingsFile))
            {
                var json = File.ReadAllText(_settingsFile);
                var loaded = JsonConvert.DeserializeObject<AppSettings>(json);
                if (loaded is not null)
                {
                    Settings = loaded;
                    _log.Info("Settings loaded successfully.");
                    WallpaperStorageService.MigrateExistingWallpapers(Settings.Wallpapers);
                    Save();
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error("Failed to load settings, using defaults.", ex);
        }

        Settings = new AppSettings();
        Save();
    }

    /// <summary>
    /// Saves current settings to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            File.WriteAllText(_settingsFile, json);
        }
        catch (Exception ex)
        {
            _log.Error("Failed to save settings.", ex);
        }
    }

    /// <summary>
    /// Resets settings to defaults and saves.
    /// </summary>
    public void Reset()
    {
        Settings = new AppSettings();
        Save();
        _log.Info("Settings reset to defaults.");
    }
}
