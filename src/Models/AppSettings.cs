namespace Livy.Models;

/// <summary>
/// Application settings, serialized to JSON.
/// </summary>
public class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = false;
    public bool PauseOnFullscreen { get; set; } = true;
    public bool PauseOnBattery { get; set; } = true;
    public int Volume { get; set; } = 0;
    public bool AudioEnabled { get; set; } = false;
    public string PerformanceMode { get; set; } = "Balanced";
    public string WallpaperScaling { get; set; } = "Fill";
    public string WallpaperDisplay { get; set; } = "All";
    public int SelectedMonitorIndex { get; set; } = 0;
    public bool ConfirmBeforeDelete { get; set; } = true;
    public string Language { get; set; } = "English";
    public bool IsFirstRun { get; set; } = true;
    public string DefaultLoopMode { get; set; } = "PingPong";

    /// <summary>
    /// The ID of the currently active wallpaper, if any.
    /// </summary>
    public string? ActiveWallpaperId { get; set; }

    /// <summary>
    /// List of wallpapers in the user's library.
    /// </summary>
    public List<Wallpaper> Wallpapers { get; set; } = new();
}
