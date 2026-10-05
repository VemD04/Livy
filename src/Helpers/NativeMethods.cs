using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Livy.Helpers;

/// <summary>
/// P/Invoke declarations for Windows API calls used to embed wallpaper behind desktop icons.
/// </summary>
internal static partial class NativeMethods
{
    // --- Window hierarchy / enumeration ---

    [LibraryImport("user32.dll")]
    internal static partial nint GetDesktopWindow();

    [LibraryImport("user32.dll")]
    internal static partial nint GetShellWindow();

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint FindWindowExW(nint hwndParent, nint hwndChildAfter,
        [MarshalAs(UnmanagedType.LPWStr)] string? lpszClass,
        [MarshalAs(UnmanagedType.LPWStr)] string? lpszWindow);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint SetParent(nint hWndChild, nint hWndNewParent);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial int SetWindowLongW(nint hWnd, int nIndex, int dwNewLong);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial int GetWindowLongW(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter,
        int x, int y, int cx, int cy, uint uFlags);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint SendMessageTimeoutW(nint hWnd, uint msg,
        nint wParam, nint lParam, uint fuFlags, uint uTimeout, out nint lpdwResult);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    internal delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(nint hWnd, int nCmdShow);

    // --- Monitor enumeration ---

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumDisplayMonitors(nint hdc, nint lprcClip,
        MonitorEnumProc lpfnEnum, nint dwData);

    internal delegate bool MonitorEnumProc(nint hMonitor, nint hdcMonitor, ref RECT lprcMonitor, nint dwData);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", SetLastError = true)]
    internal static unsafe partial int GetClassNameW(nint hWnd, char* lpClassName, int nMaxCount);

    internal static unsafe string GetWindowClassName(nint hWnd)
    {
        Span<char> buffer = stackalloc char[256];
        fixed (char* ptr = buffer)
        {
            int len = GetClassNameW(hWnd, ptr, 256);
            return len > 0 ? new string(ptr, 0, len) : string.Empty;
        }
    }

    // --- Foreground window detection ---

    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(nint hWnd, out RECT lpRect);

    // --- Shell worker ---

    /// <summary>
    /// Sends a 0x052C message to the Progman window to spawn a WorkerW behind the icons.
    /// </summary>
    internal static nint SpawnWorkerW()
    {
        var progman = FindWindowExW(nint.Zero, nint.Zero, "Progman", null);
        if (progman == nint.Zero)
        {
            progman = FindWindowExW(nint.Zero, nint.Zero, null, "Program Manager");
        }
        if (progman == nint.Zero)
        {
            progman = GetShellWindow();
        }

        if (progman != nint.Zero)
        {
            SendMessageTimeoutW(progman, 0x052C, new nint(0xD), new nint(0x1), 0x0000, 1000, out _);
            SendMessageTimeoutW(progman, 0x052C, nint.Zero, nint.Zero, 0x0000, 1000, out _);
        }

        nint workerW = nint.Zero;
        EnumWindows((hWnd, _) =>
        {
            var shell = FindWindowExW(hWnd, nint.Zero, "SHELLDLL_DefView", null);
            if (shell != nint.Zero)
            {
                workerW = FindWindowExW(nint.Zero, hWnd, "WorkerW", null);
            }
            return true;
        }, nint.Zero);

        if (workerW == nint.Zero && progman != nint.Zero)
        {
            var childWorker = FindWindowExW(progman, nint.Zero, "WorkerW", null);
            if (childWorker != nint.Zero)
            {
                workerW = childWorker;
            }
        }

        return workerW != nint.Zero ? workerW : progman;
    }

    // --- Power status ---

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    // --- Memory optimization ---

    [LibraryImport("kernel32.dll")]
    internal static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetProcessWorkingSetSize(nint hProcess, nint dwMinimumWorkingSetSize, nint dwMaximumWorkingSetSize);

    // --- Game & Fullscreen State ---

    [LibraryImport("shell32.dll")]
    internal static partial int SHQueryUserNotificationState(out int pquns);

    internal const int QUNS_NOT_PRESENT = 1;
    internal const int QUNS_BUSY = 2;
    internal const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;
    internal const int QUNS_PRESENTATION_MODE = 4;
    internal const int QUNS_ACCEPTS_NOTIFICATIONS = 5;
    internal const int QUNS_QUIET_TIME = 6;
    internal const int QUNS_APP = 7;

    // --- Process info for game detection ---

    internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial nint OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(nint hObject);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool QueryFullProcessImageName(nint hProcess, uint dwFlags, char* lpExeName, ref int lpdwSize);

    internal static unsafe string GetProcessImagePath(uint pid)
    {
        var hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProcess == nint.Zero) return string.Empty;
        try
        {
            Span<char> buffer = stackalloc char[1024];
            int size = 1024;
            fixed (char* ptr = buffer)
            {
                if (QueryFullProcessImageName(hProcess, 0, ptr, ref size))
                    return new string(ptr, 0, size);
            }
            return string.Empty;
        }
        finally
        {
            CloseHandle(hProcess);
        }
    }

    // Folders where non-game executables typically live
    private static readonly string[] _systemFolders =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps"),
    ];

    // Known non-game executable names to always allow
    private static readonly HashSet<string> _knownNonGameExes = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer.exe", "notepad.exe", "notepad++.exe", "code.exe", "devenv.exe",
        "chrome.exe", "msedge.exe", "firefox.exe", "opera.exe", "brave.exe",
        "iexplore.exe", "vivaldi.exe", "Tor Browser.exe",
        "wmplayer.exe", "vlc.exe", "mpc-hc64.exe", "mpc-hc.exe", "mpc-be64.exe",
        "winword.exe", "excel.exe", "powerpnt.exe", "outlook.exe", "onenote.exe",
        "taskmgr.exe", "msiexec.exe", "cmd.exe", "powershell.exe", "pwsh.exe",
        "conhost.exe", "dllhost.exe", "svchost.exe", "SearchHost.exe",
        "StartMenuExperienceHost.exe", "ShellExperienceHost.exe",
        "ApplicationFrameHost.exe", "SystemSettings.exe", "SettingsSyncHost.exe",
        "RuntimeBroker.exe", "fontdrvhost.exe", "dwm.exe", "csrss.exe",
        "spotify.exe", "slack.exe", "discord.exe", "teams.exe", "zoom.exe",
        "mspaint.exe", "calc.exe", "snippingtool.exe", "ScreenSketch.exe",
        "Livy.exe", "LivyApp.exe",
    };

    /// <summary>
    /// Determines whether the process with the given PID is likely a game executable.
    /// A process is considered a game if it is NOT a known system/browser/office app
    /// and does NOT reside in a Windows system folder.
    /// </summary>
    internal static bool IsLikelyGameProcess(uint pid)
    {
        var path = GetProcessImagePath(pid);
        if (string.IsNullOrEmpty(path)) return false;

        var exeName = System.IO.Path.GetFileName(path);

        // Known non-game apps → NOT a game
        if (_knownNonGameExes.Contains(exeName)) return false;

        // Resides in a Windows system folder → NOT a game
        foreach (var folder in _systemFolders)
        {
            if (!string.IsNullOrEmpty(folder) && path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    // --- Constants ---

    internal const int GWL_STYLE = -16;
    internal const int GWL_EXSTYLE = -20;
    internal const int WS_CHILD = 0x40000000;
    internal const int WS_EX_TOOLWINDOW = 0x00000080;
    internal const int SW_HIDE = 0;
    internal const int SW_SHOW = 5;

    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_SHOWWINDOW = 0x0040;

    // --- Structs ---

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left, Top, Right, Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    // --- DWM Rounded Corners (Windows 11) ---

    internal enum DWMWINDOWATTRIBUTE : uint
    {
        DWMWA_WINDOW_CORNER_PREFERENCE = 33,
        DWMWA_BORDER_COLOR             = 34,
        DWMWA_CAPTION_COLOR            = 35,
        DWMWA_USE_IMMERSIVE_DARK_MODE  = 20,
        DWMWA_MICA_EFFECT              = 1029,
    }

    internal enum DWM_WINDOW_CORNER_PREFERENCE : uint
    {
        DWMWCP_DEFAULT    = 0, // Let OS decide
        DWMWCP_DONOTROUND = 1, // Never round
        DWMWCP_ROUND      = 2, // Round corners
        DWMWCP_ROUNDSMALL = 3, // Small round corners
    }

    // DWMWA_BORDER_COLOR special values
    internal const uint DWMWA_COLOR_NONE    = 0xFFFFFFFE; // No border
    internal const uint DWMWA_COLOR_DEFAULT = 0xFFFFFFFF; // System default

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmSetWindowAttribute(nint hwnd, DWMWINDOWATTRIBUTE dwAttribute, ref uint pvAttribute, int cbAttribute);

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmSetWindowAttribute(nint hwnd, DWMWINDOWATTRIBUTE dwAttribute, ref int pvAttribute, int cbAttribute);

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmExtendFrameIntoClientArea(nint hwnd, ref MARGINS pMarInset);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    /// <summary>
    /// Applies Windows 11 native rounded corners to the specified window.
    /// Falls back silently on Windows 10.
    /// </summary>
    internal static void ApplyRoundedCorners(nint hwnd, DWM_WINDOW_CORNER_PREFERENCE preference = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND)
    {
        try
        {
            var pref = (uint)preference;
            DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(uint));
        }
        catch { /* Windows 10 fallback – ignore */ }
    }

    /// <summary>
    /// Hides the DWM window border (set border color to transparent/none).
    /// </summary>
    internal static void HideDwmBorder(nint hwnd)
    {
        try
        {
            var color = DWMWA_COLOR_NONE;
            DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, ref color, sizeof(uint));
        }
        catch { }
    }
}
