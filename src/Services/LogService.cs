using System.IO;

namespace Livy.Services;

/// <summary>
/// Simple file-based logger writing to %AppData%\Livy\Logs.
/// </summary>
public sealed class LogService : IDisposable
{
    private static readonly Lazy<LogService> _instance = new(() => new LogService());
    public static LogService Instance => _instance.Value;

    private readonly string _logDir;
    private readonly string _logFile;
    private readonly object _lock = new();
    private bool _disposed;

    private LogService()
    {
        _logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Livy", "Logs");
        Directory.CreateDirectory(_logDir);
        _logFile = Path.Combine(_logDir, $"livy_{DateTime.Now:yyyyMMdd}.log");
    }

    public void Info(string message) => Write("INFO", message);
    public void Warning(string message) => Write("WARNING", message);
    public void Error(string message, Exception? ex = null)
    {
        var text = ex is null ? message : $"{message} | {ex.GetType().Name}: {ex.Message}";
        Write("ERROR", text);
    }

    private void Write(string level, string message)
    {
        if (_disposed) return;
        try
        {
            lock (_lock)
            {
                File.AppendAllText(_logFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging should never crash the app
        }
    }

    /// <summary>
    /// Clean up log files older than 7 days.
    /// </summary>
    public void CleanOldLogs(int keepDays = 7)
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-keepDays);
            foreach (var file in Directory.GetFiles(_logDir, "livy_*.log"))
            {
                if (File.GetCreationTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
