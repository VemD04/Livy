namespace Livy.Services;

/// <summary>
/// Abstraction for video player functionality.
/// Allows the underlying video engine to be replaced in the future.
/// </summary>
public interface IVideoPlayer : IDisposable
{
    /// <summary>Starts playing a video file.</summary>
    void Play(string filePath);

    /// <summary>Pauses playback.</summary>
    void Pause();

    /// <summary>Resumes playback.</summary>
    void Resume();

    /// <summary>Stops playback and releases resources.</summary>
    void Stop();

    /// <summary>Sets volume (0-100).</summary>
    void SetVolume(int volume);

    /// <summary>Mutes or unmutes audio.</summary>
    void SetMute(bool mute);

    /// <summary>Whether the player is currently playing.</summary>
    bool IsPlaying { get; }

    /// <summary>Whether the player is paused.</summary>
    bool IsPaused { get; }

    /// <summary>Event raised when media reaches the end (for looping).</summary>
    event Action? MediaEnded;
}
