using System.IO;
using System.Windows;
using System.Windows.Media;
using Livy.Helpers;
using Newtonsoft.Json;

namespace Livy.Models;

/// <summary>
/// Represents a wallpaper entry in the user's library.
/// </summary>
public class Wallpaper : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FilePath { get; set; } = string.Empty;
    private string _fileName = string.Empty;
    public string FileName
    {
        get => string.IsNullOrEmpty(_fileName) ? string.Empty : Path.GetFileNameWithoutExtension(_fileName);
        set => SetProperty(ref _fileName, string.IsNullOrEmpty(value) ? string.Empty : Path.GetFileNameWithoutExtension(value));
    }
    public string Extension { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public double Duration { get; set; }
    public double Fps { get; set; }
    public DateTime AddedDate { get; set; } = DateTime.Now;

    private string _loopMode = "PingPong";
    public string LoopMode
    {
        get => _loopMode;
        set => SetProperty(ref _loopMode, value);
    }

    private ImageSource? _thumbnail;
    private bool _isLoadingThumbnail;

    [JsonIgnore]
    public ImageSource? Thumbnail
    {
        get
        {
            if (_thumbnail is null && FileExists)
            {
                LoadThumbnailAsync();
            }
            return _thumbnail;
        }
        set => SetProperty(ref _thumbnail, value);
    }

    private bool _isActive;
    [JsonIgnore]
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    private bool _isApplying;
    [JsonIgnore]
    public bool IsApplying
    {
        get => _isApplying;
        set => SetProperty(ref _isApplying, value);
    }

    public void LoadThumbnailAsync()
    {
        if (_isLoadingThumbnail || string.IsNullOrEmpty(FilePath)) return;
        _isLoadingThumbnail = true;

        Task.Run(() =>
        {
            try
            {
                var thumb = ThumbnailHelper.GetThumbnail(FilePath, 640, 360);
                if (thumb != null)
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        Thumbnail = thumb;
                    });
                }
            }
            finally
            {
                _isLoadingThumbnail = false;
            }
        });
    }

    /// <summary>
    /// Returns a human-readable resolution string.
    /// </summary>
    public string Resolution => Width > 0 && Height > 0 ? $"{Width} × {Height}" : "Unknown";

    /// <summary>
    /// Returns a formatted duration string.
    /// </summary>
    public string DurationText
    {
        get
        {
            if (Duration <= 0) return "Unknown";
            var ts = TimeSpan.FromSeconds(Duration);
            return ts.Hours > 0 ? ts.ToString(@"hh\:mm\:ss") : ts.ToString(@"mm\:ss");
        }
    }

    /// <summary>
    /// Returns FPS text.
    /// </summary>
    public string FpsText => Fps > 0 ? $"{Fps:F0} FPS" : string.Empty;

    /// <summary>
    /// Checks whether the source file still exists on disk.
    /// </summary>
    public bool FileExists => File.Exists(FilePath);
}
