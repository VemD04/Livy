using Livy.Helpers;
using System.Runtime.InteropServices;

namespace Livy.Services;

/// <summary>
/// Provides information about connected monitors.
/// </summary>
public class MonitorService
{
    /// <summary>
    /// Returns a list of monitor rectangles (in screen coordinates).
    /// </summary>
    public List<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();

        NativeMethods.EnumDisplayMonitors(nint.Zero, nint.Zero, (nint hMonitor, nint _, ref NativeMethods.RECT _, nint _) =>
        {
            var mi = new NativeMethods.MONITORINFO
            {
                cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>()
            };

            if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
            {
                monitors.Add(new MonitorInfo
                {
                    Handle = hMonitor,
                    Left = mi.rcMonitor.Left,
                    Top = mi.rcMonitor.Top,
                    Width = mi.rcMonitor.Width,
                    Height = mi.rcMonitor.Height,
                    IsPrimary = (mi.dwFlags & 1) != 0
                });
            }
            return true;
        }, nint.Zero);

        return monitors;
    }
}

public class MonitorInfo
{
    public nint Handle { get; set; }
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Right => Left + Width;
    public int Bottom => Top + Height;
    public bool IsPrimary { get; set; }

    public string DisplayName => IsPrimary
        ? $"Primary ({Width}×{Height})"
        : $"Monitor ({Width}×{Height})";
}
