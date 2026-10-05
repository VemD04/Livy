using System.IO;
using Microsoft.Win32;

namespace Livy.Services;

/// <summary>
/// Manages Windows startup registration via the registry Run key.
/// </summary>
public class StartupService
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "Livy";
    private readonly LogService _log = LogService.Instance;

    /// <summary>
    /// Enables or disables automatic startup with Windows.
    /// </summary>
    public void SetStartWithWindows(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key is null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath) || exePath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
                {
                    var baseExe = Path.Combine(AppContext.BaseDirectory, "Livy.exe");
                    if (File.Exists(baseExe))
                    {
                        exePath = baseExe;
                    }
                }

                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\" --minimized");
                    _log.Info($"Startup with Windows enabled: {exePath}");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
                _log.Info("Startup with Windows disabled.");
            }
        }
        catch (Exception ex)
        {
            _log.Error("Failed to set startup registry.", ex);
        }
    }

    /// <summary>
    /// Checks whether startup with Windows is currently enabled.
    /// </summary>
    public bool IsStartWithWindowsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(AppName) is not null;
        }
        catch
        {
            return false;
        }
    }
}
