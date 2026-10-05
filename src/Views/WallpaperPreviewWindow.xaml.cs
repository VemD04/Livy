using System.Windows;
using System.Windows.Input;
using LibVLCSharp.Shared;
using Livy.Models;
using Livy.Services;

namespace Livy.Views;

public partial class WallpaperPreviewWindow : Window
{
    private LibVLC? _libVlc;
    private MediaPlayer? _player;
    private Media? _media;
    private readonly Wallpaper _wallpaper;
    private readonly bool _isCurrentlyActive;
    private readonly Action<Wallpaper>? _onApply;

    public WallpaperPreviewWindow(Wallpaper wallpaper, bool isCurrentlyActive, Action<Wallpaper>? onApply = null)
    {
        InitializeComponent();
        _wallpaper = wallpaper;
        _isCurrentlyActive = isCurrentlyActive;
        _onApply = onApply;

        WallpaperFileNameText.Text = wallpaper.FileName;
        TitleText.Text = $"{LocalizationService.GetString("Preview_Title", "Wallpaper Preview")} - {wallpaper.FileName}";
        ResolutionText.Text = string.IsNullOrEmpty(wallpaper.Resolution) ? "Unknown" : wallpaper.Resolution;
        FpsText.Text = string.IsNullOrEmpty(wallpaper.FpsText) ? "" : wallpaper.FpsText;
        DurationText.Text = string.IsNullOrEmpty(wallpaper.DurationText) ? "" : wallpaper.DurationText;

        // Display primary monitor info from Windows
        var monitors = new MonitorService().GetMonitors();
        var primaryMonitor = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
        if (primaryMonitor != null && primaryMonitor.Width > 0 && primaryMonitor.Height > 0)
        {
            MonitorBadge.Visibility = Visibility.Visible;
            MonitorText.Text = $"Windows: {primaryMonitor.Width}×{primaryMonitor.Height}";
        }

        if (_isCurrentlyActive)
        {
            ApplyButton.Visibility = Visibility.Collapsed;
            ActiveBadge.Visibility = Visibility.Visible;
        }
        else
        {
            ApplyButton.Visibility = Visibility.Visible;
            ActiveBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _libVlc = new LibVLC(
                "--loop",
                "--repeat",
                "--input-repeat=65535",
                "--no-video-title-show",
                "--no-osd",
                "--avcodec-hw=d3d11va",
                "--vout=direct3d11",
                "--file-caching=1000",
                "--live-caching=1000",
                "--clock-jitter=0",
                "--quiet"
            );

            var settings = SettingsService.Instance.Settings;
            var loopMode = _wallpaper.LoopMode ?? settings.DefaultLoopMode ?? "PingPong";
            var playbackPath = SeamlessLoopService.Instance.GetCachedLoopVideo(_wallpaper.FilePath, loopMode) ?? _wallpaper.FilePath;

            _media = new Media(_libVlc, playbackPath, FromType.FromPath);
            _media.AddOption(":input-repeat=65535"); // seamless repeat

            _player = new MediaPlayer(_media);

            if (settings.AudioEnabled && settings.Volume > 0)
            {
                _player.Volume = settings.Volume;
                _player.Mute = false;
            }
            else
            {
                _player.Mute = true;
            }

            // Apply aspect ratio matching the primary monitor (Windows desktop)
            var monitors = new MonitorService().GetMonitors();
            var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
            if (primary != null && primary.Width > 0 && primary.Height > 0)
            {
                switch (settings.WallpaperScaling)
                {
                    case "Stretch":
                        _player.AspectRatio = $"{primary.Width}:{primary.Height}";
                        _player.CropGeometry = null;
                        _player.Scale = 0;
                        break;
                    case "Center":
                        _player.AspectRatio = null;
                        _player.CropGeometry = null;
                        _player.Scale = 1.0f;
                        break;
                    case "Fit":
                        _player.AspectRatio = null;
                        _player.CropGeometry = null;
                        _player.Scale = 0;
                        break;
                    default: // Fill
                        _player.AspectRatio = null;
                        _player.CropGeometry = $"{primary.Width}:{primary.Height}";
                        _player.Scale = 0;
                        break;
                }
            }

            // Seamless replay loop without clearing to black
            _player.EndReached += (s, ev) =>
            {
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        _player?.Play();
                    }
                    catch { }
                });
            };

            VideoView.MediaPlayer = _player;
            _player.Play();
        }
        catch (Exception ex)
        {
            LogService.Instance.Error("Failed to play preview video in WallpaperPreviewWindow", ex);
            LoadingText.Text = LocalizationService.GetString("Wallpapers_CannotPlay", "Wallpaper cannot be played.");
            LoadingText.Visibility = Visibility.Visible;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!ApplyButton.IsEnabled) return;
        ApplyButton.IsEnabled = false;
        ApplySpinner.Visibility = Visibility.Visible;
        ApplyButtonText.Text = LocalizationService.GetString("Wallpapers_Applying", "Applying...");

        _onApply?.Invoke(_wallpaper);

        await Task.Delay(350);
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        try
        {
            if (_player != null)
            {
                _player.Stop();
                VideoView.MediaPlayer = null;
                _player.Dispose();
                _player = null;
            }
            _media?.Dispose();
            _media = null;
            _libVlc?.Dispose();
            _libVlc = null;
        }
        catch (Exception ex)
        {
            LogService.Instance.Error("Error cleaning up WallpaperPreviewWindow player.", ex);
        }
    }
}
