using System.Windows;
using System.Windows.Interop;

namespace Livy.Helpers;

/// <summary>
/// Helper utilities for WPF window operations.
/// </summary>
internal static class WindowHelper
{
    /// <summary>
    /// Gets the Win32 window handle (HWND) for a WPF Window.
    /// </summary>
    internal static nint GetHandle(Window window)
    {
        var helper = new WindowInteropHelper(window);
        return helper.EnsureHandle();
    }

    /// <summary>
    /// Checks whether any application window is currently truly fullscreen on any monitor.
    /// Excludes the desktop, taskbar, shell windows, and standard maximized windows with title bars.
    /// </summary>
    internal static bool IsAnyWindowFullscreen()
    {
        var fgHwnd = NativeMethods.GetForegroundWindow();
        if (fgHwnd == nint.Zero) return false;

        // Ignore Desktop, Shell, and Taskbar windows
        var className = NativeMethods.GetWindowClassName(fgHwnd);
        if (string.Equals(className, "Progman", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(className, "WorkerW", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(className, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(className, "Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(className, "Windows.UI.Core.CoreWindow", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(className, "XamlExplorerHostIslandWindow", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Ignore Livy's own windows
        NativeMethods.GetWindowThreadProcessId(fgHwnd, out var fgPid);
        if (fgPid == Environment.ProcessId)
        {
            return false;
        }

        // Check window style: standard maximized window with caption is NOT fullscreen
        var style = NativeMethods.GetWindowLongW(fgHwnd, NativeMethods.GWL_STYLE);
        const int WS_CAPTION = 0x00C00000;
        const int WS_MAXIMIZE = 0x01000000;
        if ((style & WS_CAPTION) == WS_CAPTION && (style & WS_MAXIMIZE) == WS_MAXIMIZE)
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(fgHwnd, out var windowRect)) return false;

        bool isFullscreen = false;
        NativeMethods.EnumDisplayMonitors(nint.Zero, nint.Zero, (nint hMonitor, nint _, ref NativeMethods.RECT _, nint _) =>
        {
            var mi = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
            {
                if (windowRect.Left <= mi.rcMonitor.Left &&
                    windowRect.Top <= mi.rcMonitor.Top &&
                    windowRect.Right >= mi.rcMonitor.Right &&
                    windowRect.Bottom >= mi.rcMonitor.Bottom)
                {
                    isFullscreen = true;
                    return false; // stop enumeration
                }
            }
            return true;
        }, nint.Zero);

        return isFullscreen;
    }

    /// <summary>
    /// Returns true if the system is running on battery power.
    /// </summary>
    internal static bool IsOnBattery()
    {
        if (NativeMethods.GetSystemPowerStatus(out var status))
        {
            return status.ACLineStatus == 0; // 0 = Offline (battery)
        }
        return false;
    }
}
