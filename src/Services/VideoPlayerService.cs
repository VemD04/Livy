using LibVLCSharp.Shared;

namespace Livy.Services;

/// <summary>
/// Video player implementation using LibVLCSharp for hardware-accelerated playback.
/// </summary>
public class VideoPlayerService : IVideoPlayer
{
    private readonly LibVLC _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;
    private readonly LogService _log = LogService.Instance;
    private bool _disposed;

    public bool IsPlaying => _mediaPlayer?.IsPlaying ?? false;
    public bool IsPaused => _mediaPlayer?.State == VLCState.Paused;

    public event Action? MediaEnded;

    /// <summary>
    /// Exposes the underlying MediaPlayer for WPF VideoView binding.
    /// </summary>
    public MediaPlayer? MediaPlayer => _mediaPlayer;

    public VideoPlayerService()
    {
        _libVlc = new LibVLC(
            "--no-video-title-show",
            "--no-osd",
            "--avcodec-hw=d3d11va",
            "--vout=direct3d11",
            "--quiet"
        );
    }

    public void Play(string filePath)
    {
        try
        {
            Stop();

            _currentMedia = new Media(_libVlc, filePath, FromType.FromPath);
            _mediaPlayer = new MediaPlayer(_currentMedia);

            _mediaPlayer.EndReached += OnEndReached;

            _mediaPlayer.Play();
            _log.Info($"Playing: {filePath}");
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to play video: {filePath}", ex);
        }
    }

    public void Pause()
    {
        if (_mediaPlayer?.CanPause == true)
        {
            _mediaPlayer.SetPause(true);
        }
    }

    public void Resume()
    {
        if (_mediaPlayer is not null && IsPaused)
        {
            _mediaPlayer.SetPause(false);
        }
    }

    public void Stop()
    {
        try
        {
            if (_mediaPlayer is not null)
            {
                _mediaPlayer.EndReached -= OnEndReached;

                // VLC requires Stop to be called from a non-VLC thread; avoid blocking UI thread
                var mp = _mediaPlayer;
                _mediaPlayer = null;
                _ = Task.Run(() =>
                {
                    try
                    {
                        mp.Stop();
                        mp.Dispose();
                    }
                    catch { }
                });
            }

            if (_currentMedia is not null)
            {
                _currentMedia.Dispose();
                _currentMedia = null;
            }
        }
        catch (Exception ex)
        {
            _log.Error("Error stopping video player.", ex);
        }
    }

    public void SetVolume(int volume)
    {
        if (_mediaPlayer is not null)
        {
            _mediaPlayer.Volume = Math.Clamp(volume, 0, 100);
        }
    }

    public void SetMute(bool mute)
    {
        if (_mediaPlayer is not null)
        {
            _mediaPlayer.Mute = mute;
        }
    }

    private void OnEndReached(object? sender, EventArgs e)
    {
        MediaEnded?.Invoke();
    }

    /// <summary>
    /// Replays the current media (for looping). Must be called from a non-VLC thread.
    /// </summary>
    public void Replay()
    {
        if (_mediaPlayer is not null && _currentMedia is not null)
        {
            Task.Run(() =>
            {
                _mediaPlayer.Stop();
                _mediaPlayer.Play(_currentMedia);
            });
        }
    }

    /// <summary>
    /// Gets video metadata (width, height, duration, fps) for a file.
    /// </summary>
    public static (int width, int height, double duration, double fps) GetVideoInfo(string filePath)
    {
        int width = 0, height = 0;
        double duration = 0, fps = 0;

        try
        {
            using var libVlc = new LibVLC("--no-video-title-show", "--no-osd");
            using var media = new Media(libVlc, filePath, FromType.FromPath);

            media.Parse(MediaParseOptions.ParseLocal).Wait(TimeSpan.FromSeconds(10));

            duration = media.Duration / 1000.0;

            foreach (var track in media.Tracks)
            {
                if (track.TrackType == TrackType.Video)
                {
                    width = (int)track.Data.Video.Width;
                    height = (int)track.Data.Video.Height;
                    if (track.Data.Video.FrameRateDen > 0)
                    {
                        fps = (double)track.Data.Video.FrameRateNum / track.Data.Video.FrameRateDen;
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Instance.Error($"Failed to read video info: {filePath}", ex);
        }

        return (width, height, duration, fps);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _libVlc.Dispose();

        GC.SuppressFinalize(this);
    }
}
