using System.IO;
using LibVLCSharp.Shared;
using Livy.Helpers;
using Livy.Views;

namespace Livy.Services;

/// <summary>
/// Core wallpaper engine. Creates windows, embeds them behind desktop icons
/// using the WorkerW technique, and manages seamless looping video playback as live wallpaper.
/// </summary>
public class WallpaperManager : IDisposable
{
    private readonly LogService _log = LogService.Instance;
    private readonly MonitorService _monitorService = new();
    private readonly SettingsService _settings = SettingsService.Instance;

    private readonly List<WallpaperWindow> _wallpaperWindows = [];
    private LibVLC? _libVlc;
    private nint _workerW;
    private bool _disposed;
    private string? _currentFilePath;
    private Timer? _retryTimer;
    private readonly Timer _performanceTimer;
    private bool _isAutoPaused;
    public bool IsAutoPaused => _isAutoPaused;
    private bool _isManualPaused;
    public bool IsManualPaused => _isManualPaused;
    public bool IsPaused => _isManualPaused || _isAutoPaused;
    public bool IsGameDetected { get; private set; }
    public string PauseReasonText { get; private set; } = string.Empty;

    public bool IsRunning => _wallpaperWindows.Count > 0;
    public string? CurrentFilePath => _currentFilePath;

    public event Action? StateChanged;

    public WallpaperManager()
    {
        _performanceTimer = new Timer(CheckPerformanceState, null, 500, 500);
    }

    /// <summary>
    /// Applies a video as live wallpaper with seamless loop optimization.
    /// </summary>
    public void ApplyWallpaper(string filePath, string? loopMode = null)
    {
        if (!File.Exists(filePath))
        {
            _log.Warning($"Wallpaper file not found: {filePath}");
            return;
        }

        RemoveWallpaper();

        _currentFilePath = filePath;
        _workerW = NativeMethods.SpawnWorkerW();

        if (_workerW == nint.Zero)
        {
            _log.Warning("Desktop window not found (desktop may be locked or starting). Scheduling retry in 2s...");
            _retryTimer?.Dispose();
            _retryTimer = new Timer(_ =>
            {
                _retryTimer?.Dispose();
                _retryTimer = null;
                if (!string.IsNullOrEmpty(_currentFilePath))
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() => ApplyWallpaper(_currentFilePath, loopMode));
                }
            }, null, 2000, Timeout.Infinite);
            return;
        }

        _retryTimer?.Dispose();
        _retryTimer = null;

        // Ensure WorkerW has WS_CLIPCHILDREN so Explorer never paints the default wallpaper over child windows!
        const int WS_CLIPCHILDREN = 0x02000000;
        var parentStyle = NativeMethods.GetWindowLongW(_workerW, NativeMethods.GWL_STYLE);
        if ((parentStyle & WS_CLIPCHILDREN) == 0)
        {
            NativeMethods.SetWindowLongW(_workerW, NativeMethods.GWL_STYLE, parentStyle | WS_CLIPCHILDREN);
        }

        _libVlc ??= new LibVLC(
            "--loop",
            "--repeat",
            "--input-repeat=65535",
            "--no-video-title-show",
            "--no-osd",
            "--avcodec-hw=d3d11va",
            "--vout=direct3d11",
            "--file-caching=1500",
            "--network-caching=1500",
            "--clock-jitter=0",
            "--no-sub-autodetect-file",
            "--quiet"
        );

        // Determine loop mode
        var effectiveMode = loopMode 
            ?? _settings.Settings.Wallpapers.FirstOrDefault(w => w.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase))?.LoopMode 
            ?? _settings.Settings.DefaultLoopMode 
            ?? "PingPong";

        // Check if seamless version is already cached
        var activePlaybackPath = SeamlessLoopService.Instance.GetCachedLoopVideo(filePath, effectiveMode) ?? filePath;

        var monitors = GetTargetMonitors();

        foreach (var monitor in monitors)
        {
            try
            {
                var wpWindow = new WallpaperHostWindow();
                wpWindow.Show();

                var hwnd = WindowHelper.GetHandle(wpWindow);

                // Set as child of WorkerW
                NativeMethods.SetParent(hwnd, _workerW);

                // Configure window styles for embedded wallpaper
                const int WS_POPUP = unchecked((int)0x80000000);
                const int WS_CHILD = 0x40000000;
                const int WS_VISIBLE = 0x10000000;
                const int WS_CAPTION = 0x00C00000;
                const int WS_THICKFRAME = 0x00040000;

                var style = NativeMethods.GetWindowLongW(hwnd, NativeMethods.GWL_STYLE);
                style &= ~WS_POPUP;
                style &= ~WS_CAPTION;
                style &= ~WS_THICKFRAME;
                style |= WS_CHILD;
                style |= WS_VISIBLE;
                NativeMethods.SetWindowLongW(hwnd, NativeMethods.GWL_STYLE, style);

                var exStyle = NativeMethods.GetWindowLongW(hwnd, NativeMethods.GWL_EXSTYLE);
                const int WS_EX_TOOLWINDOW = 0x00000080;
                const int WS_EX_NOACTIVATE = 0x08000000;
                exStyle |= WS_EX_TOOLWINDOW;
                exStyle |= WS_EX_NOACTIVATE;
                NativeMethods.SetWindowLongW(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);

                // Position to cover the monitor
                NativeMethods.SetWindowPos(hwnd, nint.Zero,
                    monitor.Left, monitor.Top, monitor.Width, monitor.Height,
                    NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

                // Create media player with seamless looping option
                var media = new Media(_libVlc, activePlaybackPath, FromType.FromPath);
                media.AddOption(":input-repeat=65535");
                var player = new MediaPlayer(media);

                // Apply aspect ratio scaling according to user settings
                ApplyScalingToPlayer(player, _settings.Settings.WallpaperScaling, monitor.Width, monitor.Height);

                player.Volume = _settings.Settings.AudioEnabled ? _settings.Settings.Volume : 0;
                player.Mute = !_settings.Settings.AudioEnabled;

                // Seamless loop replay on background thread without clearing surface to black
                player.EndReached += (s, e) =>
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try
                        {
                            // Do NOT call player.Stop()! Calling Stop() destroys the DirectX surface and causes a black flash.
                            player.Play();
                        }
                        catch (Exception ex)
                        {
                            _log.Error("Error replaying wallpaper video.", ex);
                        }
                    });
                };

                // Assign player to VideoView
                wpWindow.Dispatcher.Invoke(() =>
                {
                    wpWindow.VideoView.MediaPlayer = player;
                    player.Play();
                });

                _wallpaperWindows.Add(new WallpaperWindow
                {
                    Window = wpWindow,
                    Player = player,
                    Media = media,
                    Monitor = monitor
                });

                _log.Info($"Wallpaper applied on monitor {monitor.Left},{monitor.Top} ({monitor.Width}×{monitor.Height})");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to create wallpaper on monitor.", ex);
            }
        }

        // If seamless version wasn't ready yet, generate in background and hot-swap
        if (activePlaybackPath == filePath && effectiveMode != "Standard" && SeamlessLoopService.Instance.IsFfmpegAvailable)
        {
            var wpModel = _settings.Settings.Wallpapers.FirstOrDefault(w => w.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            Task.Run(async () =>
            {
                try
                {
                    var seamlessPath = await SeamlessLoopService.Instance.GetOrGenerateLoopVideoAsync(filePath, effectiveMode, wpModel?.Duration ?? 0);
                    if (seamlessPath != filePath && File.Exists(seamlessPath) && _currentFilePath == filePath)
                    {
                        foreach (var wp in _wallpaperWindows)
                        {
                            try
                            {
                                var newMedia = new Media(_libVlc, seamlessPath, FromType.FromPath);
                                newMedia.AddOption(":input-repeat=65535");
                                wp.Player?.Play(newMedia);
                                if (wp.Player != null && wp.Monitor != null)
                                {
                                    ApplyScalingToPlayer(wp.Player, _settings.Settings.WallpaperScaling, wp.Monitor.Width, wp.Monitor.Height);
                                }
                                wp.Media?.Dispose();
                                wp.Media = newMedia;
                            }
                            catch { }
                        }
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => StateChanged?.Invoke());
                    }
                }
                catch (Exception ex)
                {
                    _log.Error("Error hot-swapping seamless loop.", ex);
                }
            });
        }

        StateChanged?.Invoke();
    }

    /// <summary>
    /// Removes all active wallpapers.
    /// </summary>
    public void RemoveWallpaper()
    {
        foreach (var wp in _wallpaperWindows)
        {
            try
            {
                wp.Player?.Stop();
                wp.Player?.Dispose();
                wp.Media?.Dispose();
                wp.Window?.Dispatcher.Invoke(() => wp.Window?.Close());
            }
            catch (Exception ex)
            {
                _log.Error("Error removing wallpaper window.", ex);
            }
        }

        _wallpaperWindows.Clear();
        _currentFilePath = null;
        _isAutoPaused = false;
        _isManualPaused = false;
        StateChanged?.Invoke();
        App.TrimMemory();
    }

    /// <summary>
    /// Updates volume on all players.
    /// </summary>
    public void SetVolume(int volume)
    {
        foreach (var wp in _wallpaperWindows)
        {
            if (wp.Player is not null)
            {
                wp.Player.Volume = Math.Clamp(volume, 0, 100);
            }
        }
    }

    /// <summary>
    /// Mutes or unmutes all players.
    /// </summary>
    public void SetMute(bool mute)
    {
        foreach (var wp in _wallpaperWindows)
        {
            if (wp.Player is not null)
            {
                wp.Player.Mute = mute;
            }
        }
    }

    /// <summary>
    /// Toggles manual pause state of active wallpaper.
    /// </summary>
    public void ToggleManualPause()
    {
        SetManualPause(!_isManualPaused);
    }

    /// <summary>
    /// Explicitly sets manual pause state. Releases GPU/CPU resources when paused.
    /// </summary>
    public void SetManualPause(bool pause)
    {
        if (_wallpaperWindows.Count == 0 || _disposed) return;
        if (_isManualPaused == pause) return;

        _isManualPaused = pause;
        bool effectivePause = _isManualPaused || _isAutoPaused;

        foreach (var wp in _wallpaperWindows)
        {
            try
            {
                if (wp.Player is not null)
                {
                    wp.Player.SetPause(effectivePause);

                    if (effectivePause)
                    {
                        wp.Player.Mute = true;
                    }
                    else
                    {
                        wp.Player.Mute = !_settings.Settings.AudioEnabled;
                    }
                }
            }
            catch { }
        }

        if (effectivePause)
        {
            ReleaseSystemResources();
            _log.Info(_isManualPaused
                ? "[Manual Mode] Wallpaper manually paused by user. GPU/CPU resources released."
                : $"[Game/Power Mode] Wallpaper auto-paused (Reason: {PauseReasonText}). GPU/CPU resources released.");
        }
        else
        {
            _log.Info("[Manual Mode] Wallpaper resumed by user.");
        }

        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => StateChanged?.Invoke());
    }

    /// <summary>
    /// Updates scaling on all running wallpaper players without restarting playback.
    /// Call this when the scaling setting changes to avoid black flicker.
    /// </summary>
    public void SetScaling(string scaling)
    {
        foreach (var wp in _wallpaperWindows)
        {
            if (wp.Player is not null && wp.Monitor is not null)
            {
                ApplyScalingToPlayer(wp.Player, scaling, wp.Monitor.Width, wp.Monitor.Height);
            }
        }
    }

    /// <summary>
    /// Refreshes the wallpaper (e.g., after monitor changes).
    /// </summary>
    public void RefreshWallpaper()
    {
        if (_currentFilePath is not null)
        {
            var path = _currentFilePath;
            ApplyWallpaper(path);
        }
    }

    /// <summary>
    /// Applies VLC scaling properties to a media player for the given scaling mode.
    /// </summary>
    private static void ApplyScalingToPlayer(LibVLCSharp.Shared.MediaPlayer player, string? scaling, int monitorWidth, int monitorHeight)
    {
        switch (scaling)
        {
            case "Fill":
                // Force video to fill the entire screen, cropping if needed
                player.AspectRatio = null;
                player.CropGeometry = $"{monitorWidth}:{monitorHeight}";
                player.Scale = 0;
                break;

            case "Stretch":
                // Stretch video to fill the screen ignoring aspect ratio
                player.AspectRatio = $"{monitorWidth}:{monitorHeight}";
                player.CropGeometry = null;
                player.Scale = 0;
                break;

            case "Fit":
                // Fit entire video into screen with letterbox/pillarbox
                player.AspectRatio = null;
                player.CropGeometry = null;
                player.Scale = 0;
                break;

            case "Center":
                // Display at original size, centered (no scaling)
                player.AspectRatio = null;
                player.CropGeometry = null;
                player.Scale = 1.0f;
                break;

            default:
                // Default Fill behavior
                player.AspectRatio = null;
                player.CropGeometry = $"{monitorWidth}:{monitorHeight}";
                player.Scale = 0;
                break;
        }
    }

    private List<MonitorInfo> GetTargetMonitors()
    {
        var all = _monitorService.GetMonitors();
        if (all.Count == 0) return all;

        return _settings.Settings.WallpaperDisplay switch
        {
            "Primary" => [.. all.Where(m => m.IsPrimary)],
            "Select" => all.Count > _settings.Settings.SelectedMonitorIndex
                ? [all[_settings.Settings.SelectedMonitorIndex]]
                : [all[0]],
            _ => all // "All"
        };
    }

    private void CheckPerformanceState(object? state)
    {
        if (_wallpaperWindows.Count == 0 || _disposed) return;

        bool shouldPause = false;
        bool isGame = false;
        string reason = string.Empty;

        // 1. Check battery status
        if (_settings.Settings.PauseOnBattery)
        {
            if (NativeMethods.GetSystemPowerStatus(out var powerStatus) && powerStatus.ACLineStatus == 0)
            {
                shouldPause = true;
                reason = "Battery";
            }
        }

        // 2. Check full-screen games & heavy 3D applications
        if (!shouldPause && _settings.Settings.PauseOnFullscreen)
        {
            // A. Windows Shell Notification State (Direct3D / DirectX Exclusive Fullscreen)
            //    This reliably detects exclusive-fullscreen DirectX/Vulkan games (e.g., Valorant, CS2, Apex).
            //    These bypass DWM entirely so no window rect check is possible.
            if (NativeMethods.SHQueryUserNotificationState(out var quns) == 0 &&
                quns == NativeMethods.QUNS_RUNNING_D3D_FULL_SCREEN)
            {
                shouldPause = true;
                isGame = true;
                reason = "DirectX Exclusive Fullscreen Game";
            }

            // B. Borderless Fullscreen / Windowed Fullscreen Game Detection
            //    Flowchart logic:
            //    1. Get foreground window
            //    2. If it's a desktop/taskbar/shell window → Wallpaper ON (skip)
            //    3. Check if the process is likely a game (not browser/office/system)
            //       → Not a game? Wallpaper ON (skip)
            //    4. Check if the window covers the entire monitor (fullscreen)
            //       → Not fullscreen? Wallpaper ON (skip)
            //       → Fullscreen? Wallpaper PAUSE
            if (!shouldPause)
            {
                var fgHwnd = NativeMethods.GetForegroundWindow();
                if (fgHwnd != nint.Zero)
                {
                    var className = NativeMethods.GetWindowClassName(fgHwnd);

                    // Step 1: Skip desktop / shell / taskbar windows → Wallpaper ON
                    bool isDesktopWindow = className is "Progman" or "WorkerW"
                        or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd"
                        or "Button" or "DV2ControlHost";

                    if (!isDesktopWindow)
                    {
                        NativeMethods.GetWindowThreadProcessId(fgHwnd, out var fgPid);

                        // Skip Livy's own process
                        if (fgPid > 0 && fgPid != (uint)Environment.ProcessId)
                        {
                            // Step 2: Check if the process is likely a GAME
                            //         If not a game → Wallpaper ON (skip)
                            bool likelyGame = NativeMethods.IsLikelyGameProcess(fgPid);
                            if (likelyGame)
                            {
                                // Step 3: Check if the game window covers the full monitor (fullscreen)
                                //         If not fullscreen → Wallpaper ON (skip)
                                //         If fullscreen     → Wallpaper PAUSE
                                if (NativeMethods.GetWindowRect(fgHwnd, out var fgRect))
                                {
                                    foreach (var wp in _wallpaperWindows)
                                    {
                                        if (wp.Monitor is not null)
                                        {
                                            const int margin = 4;
                                            bool coversMonitor =
                                                fgRect.Left   <= wp.Monitor.Left   + margin &&
                                                fgRect.Top    <= wp.Monitor.Top    + margin &&
                                                fgRect.Right  >= wp.Monitor.Right  - margin &&
                                                fgRect.Bottom >= wp.Monitor.Bottom - margin;

                                            if (coversMonitor)
                                            {
                                                shouldPause = true;
                                                isGame = true;
                                                reason = "Game Fullscreen Terdeteksi";
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Apply pause or resume
        if (shouldPause != _isAutoPaused)
        {
            _isAutoPaused = shouldPause;
            IsGameDetected = shouldPause && isGame;
            PauseReasonText = shouldPause ? reason : string.Empty;

            if (!_isManualPaused)
            {
                foreach (var wp in _wallpaperWindows)
                {
                    try
                    {
                        if (wp.Player is not null)
                        {
                            wp.Player.SetPause(_isAutoPaused);

                            // When paused, mute audio to release audio decoding thread
                            if (_isAutoPaused)
                            {
                                wp.Player.Mute = true;
                            }
                            else
                            {
                                wp.Player.Mute = !_settings.Settings.AudioEnabled;
                            }
                        }
                    }
                    catch { }
                }

                if (_isAutoPaused)
                {
                    ReleaseSystemResources();
                    _log.Info($"[Game/Power Mode] Wallpaper auto-paused (Reason: {PauseReasonText}). GPU/CPU resources released.");
                }
                else
                {
                    _log.Info("[Game/Power Mode] Game closed / Desktop active. Wallpaper automatically resumed.");
                }
            }

            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => StateChanged?.Invoke());
        }
    }

    private static void ReleaseSystemResources()
    {
        try
        {
            NativeMethods.SetProcessWorkingSetSize(NativeMethods.GetCurrentProcess(), -1, -1);
            GC.Collect(1, GCCollectionMode.Optimized);
        }
        catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _performanceTimer?.Dispose();
        RemoveWallpaper();
        _retryTimer?.Dispose();
        _libVlc?.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Holds references to a single wallpaper window and its associated player.
    /// </summary>
    private class WallpaperWindow
    {
        public Views.WallpaperHostWindow? Window { get; set; }
        public MediaPlayer? Player { get; set; }
        public Media? Media { get; set; }
        public MonitorInfo? Monitor { get; set; }
    }
}
