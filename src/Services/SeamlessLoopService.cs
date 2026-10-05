using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Livy.Services;

/// <summary>
/// Service that optimizes videos into seamless loops (Ping-Pong / Boomerang and Crossfade)
/// eliminating visual jumps and frame stutter when animations don't naturally return to start.
/// </summary>
public sealed class SeamlessLoopService
{
    private static readonly Lazy<SeamlessLoopService> _instance = new(() => new SeamlessLoopService());
    public static SeamlessLoopService Instance => _instance.Value;

    private readonly LogService _log = LogService.Instance;
    private readonly string _cacheDir;
    private bool? _isFfmpegAvailable;
    private readonly ConcurrentDictionary<string, Task<string>> _inFlightTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _renderSemaphore = new(1, 1);
    private readonly Lock _processLock = new();
    private readonly HashSet<Process> _activeProcesses = [];

    private SeamlessLoopService()
    {
        _cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Livy", "Cache", "Loops");
        try
        {
            Directory.CreateDirectory(_cacheDir);
        }
        catch (Exception ex)
        {
            _log.Error("Failed to create loop cache directory.", ex);
        }
    }

    /// <summary>
    /// Checks whether ffmpeg is available on the system PATH.
    /// </summary>
    public bool IsFfmpegAvailable
    {
        get
        {
            if (_isFfmpegAvailable.HasValue) return _isFfmpegAvailable.Value;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = "-version",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit(3000);
                    _isFfmpegAvailable = proc.ExitCode == 0;
                    return _isFfmpegAvailable.Value;
                }
            }
            catch
            {
                _isFfmpegAvailable = false;
            }

            _isFfmpegAvailable ??= false;
            return _isFfmpegAvailable.Value;
        }
    }

    /// <summary>
    /// Returns the cached seamless video path if already generated, otherwise null.
    /// </summary>
    public string? GetCachedLoopVideo(string sourcePath, string mode)
    {
        if (mode == "Standard" || !File.Exists(sourcePath)) return sourcePath;
        var cachePath = GetCacheFilePath(sourcePath, mode);
        return File.Exists(cachePath) && new FileInfo(cachePath).Length > 0 ? cachePath : null;
    }

    /// <summary>
    /// Asynchronously gets or generates an optimized seamless loop video.
    /// </summary>
    public Task<string> GetOrGenerateLoopVideoAsync(string sourcePath, string mode, double duration = 0)
    {
        if (mode == "Standard" || !IsFfmpegAvailable || !File.Exists(sourcePath))
        {
            return Task.FromResult(sourcePath);
        }

        var cached = GetCachedLoopVideo(sourcePath, mode);
        if (cached != null)
        {
            return Task.FromResult(cached);
        }

        var outputPath = GetCacheFilePath(sourcePath, mode);
        var cacheKey = $"{sourcePath}_{mode}";

        return _inFlightTasks.GetOrAdd(cacheKey, k => Task.Run(async () =>
        {
            try
            {
                if (File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
                {
                    return outputPath;
                }

                _log.Info($"Generating seamless {mode} loop for: {Path.GetFileName(sourcePath)}");

                // Probe duration if not provided
                if (duration <= 0)
                {
                    duration = ProbeDuration(sourcePath);
                }

                // Throttle: Ensure only one FFmpeg loop generation runs at a time
                await _renderSemaphore.WaitAsync();
                bool success = false;
                try
                {
                    success = await Task.Run(() => GenerateLoopVideoInternal(sourcePath, outputPath, mode, duration));
                }
                finally
                {
                    _renderSemaphore.Release();
                }

                if (success && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0)
                {
                    _log.Info($"Seamless loop generated successfully: {outputPath}");
                    return outputPath;
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to generate seamless loop for {sourcePath}.", ex);
            }
            finally
            {
                _inFlightTasks.TryRemove(cacheKey, out _);
            }

            // Fallback to source video if generation fails
            return sourcePath;
        }));
    }

    private bool GenerateLoopVideoInternal(string inputPath, string outputPath, string mode, double duration)
    {
        var tempOutput = Path.Combine(_cacheDir, $"{Guid.NewGuid():N}.tmp.mp4");
        try
        {
            string filterComplex;

            // Ensure dimensions do not exceed standard desktop 1080p (1920x1080) and are divisible by 2.
            // Uses bicubic interpolation for sharp, anti-aliased downscaling.
            const string scaleFilter = "scale='min(1920,iw)':'min(1080,ih)':force_original_aspect_ratio=decrease:flags=bicubic,scale=trunc(iw/2)*2:trunc(ih/2)*2";

            if (mode == "PingPong")
            {
                // Smooth Eased Ping-Pong: 1 second before the end (both forward apex and reverse trough),
                // smoothly blend into the reverse direction so motion gently decelerates and turns around
                // naturally without abrupt stutter or jerking.
                var d = duration > 0 ? duration : 10.0;
                var tBlend = Math.Min(1.0, Math.Max(0.3, d * 0.15));

                var off1 = (d - tBlend).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
                var off2 = (2 * d - 3 * tBlend).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
                var bStr = tBlend.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);

                filterComplex = $"[0:v]{scaleFilter}[scaled];" +
                                $"[scaled]split=2[fwd][rev_in];" +
                                $"[rev_in]reverse[rev];" +
                                $"[fwd][rev]xfade=transition=fade:duration={bStr}:offset={off1}[fwd_rev];" +
                                $"[fwd_rev]split=2[fr1][fr2];" +
                                $"[fr1]trim=start={bStr},setpts=PTS-STARTPTS[fr_main];" +
                                $"[fr2]trim=start=0:end={bStr},setpts=PTS-STARTPTS[fr_head];" +
                                $"[fr_main][fr_head]xfade=transition=fade:duration={bStr}:offset={off2}[v]";
            }
            else if (mode == "Crossfade" || mode == "Seamless")
            {
                // Seamless Crossfade: Tail dissolves smoothly into the beginning
                var d = duration > 0 ? duration : 10.0;
                var b = Math.Min(1.8, Math.Max(0.6, d * 0.12));
                var offset = Math.Max(0.1, d - b);

                var bStr = b.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                var dStr = d.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                var offStr = offset.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

                filterComplex = $"[0:v]{scaleFilter}[scaled];[scaled]split=2[v1][v2];[v1]trim=start={bStr}:end={dStr},setpts=PTS-STARTPTS[main];[v2]trim=start=0:end={bStr},setpts=PTS-STARTPTS[head];[main][head]xfade=transition=fade:duration={bStr}:offset={offStr}[v]";
            }
            else
            {
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            psi.ArgumentList.Add("-y");
            psi.ArgumentList.Add("-loglevel");
            psi.ArgumentList.Add("error");
            psi.ArgumentList.Add("-nostats");
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(inputPath);
            psi.ArgumentList.Add("-filter_complex");
            psi.ArgumentList.Add(filterComplex);
            psi.ArgumentList.Add("-map");
            psi.ArgumentList.Add("[v]");
            psi.ArgumentList.Add("-c:v");
            psi.ArgumentList.Add("libx264");
            psi.ArgumentList.Add("-preset");
            psi.ArgumentList.Add("veryfast");
            psi.ArgumentList.Add("-crf");
            psi.ArgumentList.Add("18");
            psi.ArgumentList.Add("-pix_fmt");
            psi.ArgumentList.Add("yuv420p");
            psi.ArgumentList.Add("-g");
            psi.ArgumentList.Add("60");

            // Cap thread count to at most half of CPU cores (between 2 and 4 threads)
            // so background rendering NEVER freezes the UI or starves the system.
            var threadCount = Math.Max(2, Math.Min(4, Environment.ProcessorCount / 2));
            psi.ArgumentList.Add("-threads");
            psi.ArgumentList.Add(threadCount.ToString());
            psi.ArgumentList.Add("-an");
            psi.ArgumentList.Add("-movflags");
            psi.ArgumentList.Add("+faststart");
            psi.ArgumentList.Add(tempOutput);

            Process? proc = null;
            try
            {
                proc = Process.Start(psi);
                if (proc == null) return false;

                // Lower process priority to BelowNormal so video playback and user UI have priority
                try
                {
                    proc.PriorityClass = ProcessPriorityClass.BelowNormal;
                }
                catch { }

                RegisterActiveProcess(proc);

                // Wait up to 300s (5 minutes) so high-resolution wallpapers finish encoding cleanly
                bool exited = proc.WaitForExit(300000);

                if (exited && proc.ExitCode == 0 && File.Exists(tempOutput) && new FileInfo(tempOutput).Length > 0)
                {
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                    File.Move(tempOutput, outputPath);
                    return true;
                }
                else
                {
                    if (!exited)
                    {
                        try { proc.Kill(); } catch { }
                        _log.Warning($"FFmpeg loop generation timed out for: {inputPath}");
                    }
                    else
                    {
                        _log.Warning($"FFmpeg loop generation returned exit code {proc.ExitCode}");
                    }
                }
            }
            finally
            {
                if (proc != null)
                {
                    UnregisterActiveProcess(proc);
                    proc.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error("Exception during FFmpeg loop generation.", ex);
        }
        finally
        {
            if (File.Exists(tempOutput))
            {
                try { File.Delete(tempOutput); } catch { }
            }
        }

        return false;
    }

    private void RegisterActiveProcess(Process proc)
    {
        lock (_processLock)
        {
            _activeProcesses.Add(proc);
        }
    }

    private void UnregisterActiveProcess(Process proc)
    {
        lock (_processLock)
        {
            _activeProcesses.Remove(proc);
        }
    }

    /// <summary>
    /// Kills any running ffmpeg processes and releases resources on application shutdown.
    /// </summary>
    public void CancelAndCleanup()
    {
        lock (_processLock)
        {
            foreach (var proc in _activeProcesses)
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill(true);
                    }
                }
                catch { }
                finally
                {
                    try { proc.Dispose(); } catch { }
                }
            }
            _activeProcesses.Clear();
        }
    }

    private static double ProbeDuration(string filePath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc != null)
            {
                var outText = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit(3000);
                if (double.TryParse(outText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
                {
                    return d;
                }
            }
        }
        catch { }

        return 10.0;
    }

    private string GetCacheFilePath(string sourcePath, string mode)
    {
        try
        {
            var fileInfo = new FileInfo(sourcePath);
            var key = $"{sourcePath}_{fileInfo.Length}_{fileInfo.LastWriteTimeUtc.Ticks}_{mode}_v4";
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
            var hashStr = Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
            return Path.Combine(_cacheDir, $"{hashStr}_{mode}.mp4");
        }
        catch
        {
            var cleanName = Path.GetFileNameWithoutExtension(sourcePath);
            return Path.Combine(_cacheDir, $"{cleanName}_{mode}.mp4");
        }
    }

    /// <summary>
    /// Deletes any cached seamless loop files generated for the given source video path.
    /// </summary>
    public void DeleteCachedLoops(string sourcePath)
    {
        try
        {
            var modes = new[] { "PingPong", "Crossfade", "Reverse" };
            foreach (var m in modes)
            {
                var p = GetCacheFilePath(sourcePath, m);
                if (File.Exists(p))
                {
                    File.Delete(p);
                    _log.Info($"Deleted cached loop: {p}");
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to delete cached loops for: {sourcePath}", ex);
        }
    }
}
