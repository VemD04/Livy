using System.IO;
using Livy.Models;

namespace Livy.Services;

/// <summary>
/// Manages local storage of wallpaper files inside the application data directory (%AppData%\Livy\Wallpapers),
/// preventing missing-file errors when original files are moved or deleted by the user.
/// </summary>
public static class WallpaperStorageService
{
    private static readonly LogService _log = LogService.Instance;

    public static string WallpapersDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Livy", "Wallpapers");

    public static void EnsureDirectoryExists()
    {
        try
        {
            if (!Directory.Exists(WallpapersDirectory))
            {
                Directory.CreateDirectory(WallpapersDirectory);
            }
        }
        catch (Exception ex)
        {
            _log.Error("Failed to create Wallpapers directory.", ex);
        }
    }

    /// <summary>
    /// Copies the wallpaper video file into Livy's local app storage so it remains available
    /// even if the original source file is deleted or moved by the user.
    /// </summary>
    public static string ImportWallpaperFile(string sourceFilePath, string wallpaperId)
    {
        EnsureDirectoryExists();

        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
        {
            return sourceFilePath;
        }

        try
        {
            var ext = Path.GetExtension(sourceFilePath);
            var safeFileName = $"{wallpaperId}{ext}";
            var destination = Path.Combine(WallpapersDirectory, safeFileName);

            // If already stored in the app's wallpapers directory, return it directly
            if (string.Equals(Path.GetFullPath(sourceFilePath), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            {
                return destination;
            }

            File.Copy(sourceFilePath, destination, overwrite: true);
            _log.Info($"Copied wallpaper to internal storage: {destination}");
            return destination;
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to copy wallpaper file to internal storage: {sourceFilePath}", ex);
            return sourceFilePath; // Fallback to original path
        }
    }

    /// <summary>
    /// Deletes the local copy of the wallpaper file and any associated seamless loop caches.
    /// </summary>
    public static void DeleteWallpaperFiles(Wallpaper wallpaper)
    {
        if (wallpaper == null) return;

        try
        {
            if (!string.IsNullOrEmpty(wallpaper.FilePath) && File.Exists(wallpaper.FilePath))
            {
                var fullPath = Path.GetFullPath(wallpaper.FilePath);
                var storageDir = Path.GetFullPath(WallpapersDirectory);

                // Only delete if the file is inside Livy's app data directory to avoid deleting user's external personal files
                if (fullPath.StartsWith(storageDir, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(fullPath);
                    _log.Info($"Deleted stored wallpaper file: {fullPath}");
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Error deleting wallpaper file: {wallpaper.FilePath}", ex);
        }

        // Clean up any cached seamless loops and thumbnail
        try
        {
            SeamlessLoopService.Instance.DeleteCachedLoops(wallpaper.FilePath);
            Helpers.ThumbnailHelper.InvalidateCache(wallpaper.FilePath);
        }
        catch { }
    }

    /// <summary>
    /// Automatically copies any existing library wallpapers that are not yet in internal storage.
    /// </summary>
    public static void MigrateExistingWallpapers(IEnumerable<Wallpaper> wallpapers)
    {
        EnsureDirectoryExists();
        foreach (var wp in wallpapers)
        {
            if (string.IsNullOrEmpty(wp.FilePath) || !File.Exists(wp.FilePath)) continue;

            try
            {
                var fullPath = Path.GetFullPath(wp.FilePath);
                var storageDir = Path.GetFullPath(WallpapersDirectory);

                if (!fullPath.StartsWith(storageDir, StringComparison.OrdinalIgnoreCase))
                {
                    var newPath = ImportWallpaperFile(wp.FilePath, wp.Id);
                    if (newPath != wp.FilePath && File.Exists(newPath))
                    {
                        wp.FilePath = newPath;
                        _log.Info($"Migrated wallpaper '{wp.FileName}' to internal storage.");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Error migrating wallpaper '{wp.FileName}':", ex);
            }
        }
    }
}
