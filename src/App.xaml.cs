using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using LibVLCSharp.Shared;
using Livy.Services;
using Livy.ViewModels;
using Livy.Views;

namespace Livy;

public partial class App : Application
{
    private static Mutex? _mutex;
    private static bool _ownsMutex;
    private static EventWaitHandle? _showEvent;
    private static RegisteredWaitHandle? _waitHandleRegistration;
    private const string ShowEventName = "Livy_ShowMainWindowEvent";
    private TaskbarIcon? _notifyIcon;
    private Views.MainWindow? _mainWindow;
    private readonly LogService _log = LogService.Instance;
    private readonly SettingsService _settings = SettingsService.Instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 0. Global Exception Handling
        DispatcherUnhandledException += (s, args) =>
        {
            _log.Error("Unhandled UI exception", args.Exception);
            ModernDialogWindow.Show($"Terjadi kesalahan: {args.Exception.Message}", "Livy Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                _log.Error("Unhandled AppDomain exception", ex);
            }
        };

        // 1. Single Instance Check
        const string appName = "Livy_SingleInstanceMutex";
        _mutex = new Mutex(true, appName, out bool createdNew);
        _ownsMutex = createdNew;

        if (!createdNew)
        {
            // Another instance is already running: signal it to bring its main window to the front
            try
            {
                using var signalEvent = EventWaitHandle.OpenExisting(ShowEventName);
                signalEvent.Set();
            }
            catch
            {
                // Fallback: if signaling failed, show friendly information dialog
                try
                {
                    _settings.Load();
                    LocalizationService.Instance.Initialize();
                    ThemeService.Instance.Initialize();
                }
                catch { }

                var msg = LocalizationService.GetString(
                    "App_AlreadyRunning",
                    "Livy sudah berjalan di latar belakang (System Tray). Klik dua kali ikon Livy di pojok kanan bawah taskbar untuk membukanya.");
                ModernDialogWindow.Show(msg, "Livy", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            _mutex.Dispose();
            _mutex = null;
            Current.Shutdown();
            return;
        }

        // Setup activation event listener for subsequent instance requests
        try
        {
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            _waitHandleRegistration = ThreadPool.RegisterWaitForSingleObject(_showEvent, (state, timedOut) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    ShowMainWindow();
                });
            }, null, Timeout.Infinite, false);
        }
        catch (Exception ex)
        {
            _log.Warning($"Failed to create show event wait handle: {ex.Message}");
        }

        // 2. Load Settings & Initialize Core
        _settings.Load();
        ThemeService.Instance.Initialize();
        LocalizationService.Instance.Initialize();
        _log.CleanOldLogs();
        _log.Info("Livy starting...");

        try
        {
            Core.Initialize();
        }
        catch (Exception ex)
        {
            _log.Error("Failed to initialize LibVLC.", ex);
            ModernDialogWindow.Show("Failed to initialize video engine. Livy will exit.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Current.Shutdown();
            return;
        }

        // 3. Create System Tray Icon
        System.IO.Stream? iconStream = null;
        try
        {
            iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/AppIcon.ico"))?.Stream;
        }
        catch { }

        var trayMenu = (ContextMenu)FindResource("TrayMenu");
        trayMenu.Opened += (s, args) => UpdateTrayMenu(trayMenu);

        _notifyIcon = new TaskbarIcon
        {
            Icon = iconStream != null ? new System.Drawing.Icon(iconStream) : null,
            IconSource = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/AppIcon.ico")),
            ToolTipText = "Livy",
            ContextMenu = trayMenu
        };
        iconStream?.Dispose();
        _notifyIcon.TrayMouseDoubleClick += (s, args) => ShowMainWindow();

        base.OnStartup(e);

        // 4. Create Main Window
        _mainWindow = new Views.MainWindow();

        if (_mainWindow?.DataContext is MainViewModel mainVm)
        {
            mainVm.WallpaperManager.StateChanged += () =>
            {
                Dispatcher.BeginInvoke(() => UpdateTrayMenu(trayMenu));
            };
        }
        UpdateTrayMenu(trayMenu);

        // 5. Check startup arguments
        bool startMinimized = e.Args.Contains("--minimized") || _settings.Settings.StartMinimized;

        if (!startMinimized)
        {
            ShowMainWindow();
        }
        else
        {
            TrimMemory();
        }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null) return;
        
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }
        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
        _mainWindow.Focus();
    }

    private void TrayOpen_Click(object sender, RoutedEventArgs e)
    {
        ShowMainWindow();
    }

    private void TrayPause_Click(object sender, RoutedEventArgs e)
    {
        var wm = (_mainWindow?.DataContext as MainViewModel)?.WallpaperManager;
        if (wm is null || !wm.IsRunning) return;

        wm.ToggleManualPause();
        UpdateTrayMenu();
    }

    private void UpdateTrayMenu(ContextMenu? menu = null)
    {
        menu ??= _notifyIcon?.ContextMenu;
        if (menu is null) return;

        var pauseItem = menu.Items.OfType<MenuItem>().FirstOrDefault(i => i.Name == "TrayPauseItem" || (string)i.Tag == "Pause");
        if (pauseItem is null) return;

        var wm = (_mainWindow?.DataContext as MainViewModel)?.WallpaperManager;
        if (wm is null || !wm.IsRunning)
        {
            pauseItem.IsEnabled = false;
            pauseItem.Header = LocalizationService.GetString("Tray_Pause", "Jeda Wallpaper");
        }
        else
        {
            pauseItem.IsEnabled = true;
            if (wm.IsPaused)
            {
                pauseItem.Header = LocalizationService.GetString("Tray_Resume", "Lanjutkan Wallpaper");
            }
            else
            {
                pauseItem.Header = LocalizationService.GetString("Tray_Pause", "Jeda Wallpaper");
            }
        }
    }


    private void TraySettings_Click(object sender, RoutedEventArgs e)
    {
        ShowMainWindow();
        if (_mainWindow?.DataContext is MainViewModel vm)
        {
            vm.NavigateSettingsCommand.Execute(null);
        }
    }

    private void TrayExit_Click(object sender, RoutedEventArgs e)
    {
        _log.Info("Livy exiting normally.");
        
        if (_mainWindow?.DataContext is MainViewModel vm)
        {
            vm.WallpaperManager.Dispose();
        }

        _notifyIcon?.Dispose();
        Current.Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Cancel and kill any background ffmpeg process so no zombie processes are left behind
        SeamlessLoopService.Instance.CancelAndCleanup();

        // Ensure wallpaper window and video player are cleanly disposed
        if (_mainWindow?.DataContext is MainViewModel vm)
        {
            try
            {
                vm.WallpaperManager.Dispose();
            }
            catch { }
        }

        _notifyIcon?.Dispose();

        _waitHandleRegistration?.Unregister(null);
        _showEvent?.Dispose();
        _showEvent = null;

        if (_ownsMutex && _mutex is not null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex wasn't owned by this thread
            }
            finally
            {
                _mutex.Dispose();
                _mutex = null;
            }
        }

        _log.Dispose();
        
        base.OnExit(e);
    }

    /// <summary>
    /// Minimizes process working set gently without evicting active video buffers to disk.
    /// </summary>
    public static void TrimMemory()
    {
        try
        {
            GC.Collect(1, GCCollectionMode.Optimized, false);
        }
        catch { }
    }
}
