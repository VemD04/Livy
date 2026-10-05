using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Livy.Helpers;
using Livy.Models;
using Livy.Services;
using Livy.Views;
using Microsoft.Win32;

namespace Livy.ViewModels;

/// <summary>
/// Unified ViewModel for Home and Wallpaper management.
/// </summary>
public class HomeViewModel : ObservableObject
{
    private readonly WallpaperManager _wallpaperManager;
    private readonly SettingsService _settings = SettingsService.Instance;
    private readonly LogService _log = LogService.Instance;

    // Active Wallpaper Status
    private string _wallpaperName = "No wallpaper selected";
    private string _resolution = "";
    private string _fps = "";
    private string _duration = "";
    private string _status = "Stopped";
    private string _statusColor = "#9CA3AF";
    private bool _hasActiveWallpaper;
    private Wallpaper? _activeWallpaper;

    public string WallpaperName { get => _wallpaperName; set => SetProperty(ref _wallpaperName, value); }
    public string Resolution { get => _resolution; set => SetProperty(ref _resolution, value); }
    public string Fps { get => _fps; set => SetProperty(ref _fps, value); }
    public string Duration { get => _duration; set => SetProperty(ref _duration, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public string StatusColor { get => _statusColor; set => SetProperty(ref _statusColor, value); }
    public bool HasActiveWallpaper { get => _hasActiveWallpaper; set => SetProperty(ref _hasActiveWallpaper, value); }
    public Wallpaper? ActiveWallpaper { get => _activeWallpaper; set => SetProperty(ref _activeWallpaper, value); }

    public WallpaperManager WallpaperManager => _wallpaperManager;
    public bool IsAutoPaused => _wallpaperManager.IsAutoPaused;
    public bool IsPaused => _wallpaperManager.IsPaused;
    public bool IsManualPaused => _wallpaperManager.IsManualPaused;
    public bool IsGameDetected => _wallpaperManager.IsGameDetected;

    public string? ActiveWallpaperVideoPath
    {
        get
        {
            if (ActiveWallpaper is null || !ActiveWallpaper.FileExists) return null;
            return SeamlessLoopService.Instance.GetCachedLoopVideo(ActiveWallpaper.FilePath, ActiveLoopMode) ?? ActiveWallpaper.FilePath;
        }
    }

    // Wallpaper Library
    public ObservableCollection<Wallpaper> Wallpapers { get; } = [];

    private Wallpaper? _selectedWallpaper;
    public Wallpaper? SelectedWallpaper
    {
        get => _selectedWallpaper;
        set
        {
            if (SetProperty(ref _selectedWallpaper, value))
            {
                HasSelection = value is not null;
                OnPropertyChanged(nameof(CanApplySelectedWallpaper));
                PreviewWallpaperCommand?.RaiseCanExecuteChanged();
                ApplyWallpaperCommand?.RaiseCanExecuteChanged();
                DeleteWallpaperCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    private bool _isApplyingAny;
    public bool IsApplyingAny
    {
        get => _isApplyingAny;
        set
        {
            if (SetProperty(ref _isApplyingAny, value))
            {
                OnPropertyChanged(nameof(CanApplySelectedWallpaper));
                ApplyWallpaperCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanApplySelectedWallpaper => SelectedWallpaper is not null && !SelectedWallpaper.IsActive && !IsApplyingAny;

    private bool _hasSelection;
    public bool HasSelection
    {
        get => _hasSelection;
        set => SetProperty(ref _hasSelection, value);
    }

    // Loop Mode
    private string _activeLoopMode = "PingPong";
    public string ActiveLoopMode
    {
        get => _activeLoopMode;
        set => SetProperty(ref _activeLoopMode, value);
    }

    private bool _isOptimizing;
    public bool IsOptimizing
    {
        get => _isOptimizing;
        set => SetProperty(ref _isOptimizing, value);
    }

    public static bool IsFfmpegAvailable => SeamlessLoopService.Instance.IsFfmpegAvailable;

    // Commands
    public RelayCommand RemoveCommand { get; }
    public RelayCommand AddWallpaperCommand { get; }
    public RelayCommand PreviewWallpaperCommand { get; }
    public RelayCommand ApplyWallpaperCommand { get; }
    public RelayCommand EditWallpaperCommand { get; }
    public RelayCommand DeleteWallpaperCommand { get; }
    public RelayCommand SetLoopModeCommand { get; }

    public HomeViewModel(WallpaperManager wallpaperManager)
    {
        _wallpaperManager = wallpaperManager;
        _wallpaperManager.StateChanged += Refresh;
        LocalizationService.Instance.LanguageChanged += Refresh;

        RemoveCommand = new RelayCommand(
            () =>
            {
                _wallpaperManager.RemoveWallpaper();
                _settings.Settings.ActiveWallpaperId = null;
                _settings.Save();
                Refresh();
            },
            () => HasActiveWallpaper
        );

        AddWallpaperCommand = new RelayCommand(AddWallpaper);

        PreviewWallpaperCommand = new RelayCommand(
            () =>
            {
                if (SelectedWallpaper is not null)
                {
                    PreviewWallpaper(SelectedWallpaper);
                }
            },
            () => HasSelection
        );

        ApplyWallpaperCommand = new RelayCommand(
            () =>
            {
                if (SelectedWallpaper is not null && CanApplySelectedWallpaper)
                {
                    ApplyWallpaper(SelectedWallpaper);
                }
            },
            () => CanApplySelectedWallpaper
        );

        EditWallpaperCommand = new RelayCommand(
            () =>
            {
                if (SelectedWallpaper is not null)
                {
                    EditWallpaper(SelectedWallpaper);
                }
            },
            () => HasSelection
        );

        DeleteWallpaperCommand = new RelayCommand(
            () =>
            {
                if (SelectedWallpaper is not null)
                {
                    DeleteWallpaper(SelectedWallpaper);
                }
            },
            () => HasSelection
        );

        SetLoopModeCommand = new RelayCommand(p => SetLoopMode(p?.ToString()));

        Refresh();
    }

    public void Refresh()
    {
        var activeId = _settings.Settings.ActiveWallpaperId;
        var wallpaper = activeId is not null
            ? _settings.Settings.Wallpapers.FirstOrDefault(w => w.Id == activeId)
            : null;

        ActiveWallpaper = wallpaper;
        ActiveLoopMode = wallpaper?.LoopMode ?? _settings.Settings.DefaultLoopMode ?? "PingPong";

        if (wallpaper is not null && wallpaper.FileExists)
        {
            HasActiveWallpaper = true;
            WallpaperName = wallpaper.FileName;
            Resolution = wallpaper.Resolution;
            Fps = wallpaper.FpsText;
            Duration = wallpaper.DurationText;

            if (_wallpaperManager.IsRunning)
            {
                if (_wallpaperManager.IsPaused)
                {
                    Status = _wallpaperManager.IsGameDetected
                        ? LocalizationService.GetString("Home_StatusGamePaused", "Game Terdeteksi (Dijeda - GPU/CPU Dilepas)")
                        : LocalizationService.GetString("Home_StatusPaused", "Dijeda");
                    StatusColor = "#EAB308";
                }
                else
                {
                    Status = LocalizationService.GetString("Home_StatusRunning", "Berjalan");
                    StatusColor = "#22C55E";
                }
            }
            else
            {
                Status = LocalizationService.GetString("Home_StatusStopped", "Stopped");
                StatusColor = "#9CA3AF";
            }
        }
        else
        {
            HasActiveWallpaper = false;
            WallpaperName = LocalizationService.GetString("Home_NoWallpaperSelected", "No wallpaper selected");
            Resolution = "";
            Fps = "";
            Duration = "";
            Status = LocalizationService.GetString("Home_StatusStopped", "Stopped");
            StatusColor = "#9CA3AF";

            if (wallpaper is not null && !wallpaper.FileExists)
            {
                WallpaperName = LocalizationService.GetString("Wallpapers_FileNotFound", "File not found");
                Status = LocalizationService.GetString("Wallpapers_FileNotFound", "File not found");
                StatusColor = "#EF4444";
            }
        }

        // Refresh library collection
        Wallpapers.Clear();
        foreach (var wp in _settings.Settings.Wallpapers)
        {
            wp.IsActive = (wp.Id == activeId);
            Wallpapers.Add(wp);
        }
        OnPropertyChanged(nameof(IsAutoPaused));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsManualPaused));
        OnPropertyChanged(nameof(IsGameDetected));
        OnPropertyChanged(nameof(CanApplySelectedWallpaper));
        OnPropertyChanged(nameof(ActiveWallpaperVideoPath));
        PreviewWallpaperCommand?.RaiseCanExecuteChanged();
        ApplyWallpaperCommand?.RaiseCanExecuteChanged();
        DeleteWallpaperCommand?.RaiseCanExecuteChanged();
    }

    public void AddWallpaper()
    {
        var win = new Views.AddWallpaperWindow();
        if (System.Windows.Application.Current.MainWindow is not null)
        {
            win.Owner = System.Windows.Application.Current.MainWindow;
        }

        if (win.ShowDialog() == true && win.AddedWallpapers.Count > 0)
        {
            foreach (var wp in win.AddedWallpapers)
            {
                if (!Wallpapers.Contains(wp))
                {
                    Wallpapers.Add(wp);
                }
            }

            var newlyAdded = win.AddedWallpapers.LastOrDefault();
            if (newlyAdded is not null)
            {
                SelectedWallpaper = newlyAdded;
            }

            _settings.Save();
        }
    }

    public async void ApplyWallpaper(Wallpaper wallpaper)
    {
        if (wallpaper is null) return;
        if (wallpaper.IsApplying || IsApplyingAny) return;

        if (!wallpaper.FileExists)
        {
            var msg = LocalizationService.GetString("Wallpapers_FileNotFound", "Wallpaper file not found.");
            ModernDialogWindow.Show(
                msg,
                "Livy", MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            wallpaper.IsApplying = true;
            IsApplyingAny = true;
            await Task.Yield();

            // Apply wallpaper to desktop immediately without waiting!
            // WallpaperManager handles background loop generation and hot-swaps seamlessly when ready.
            _wallpaperManager.ApplyWallpaper(wallpaper.FilePath, wallpaper.LoopMode);
            _settings.Settings.ActiveWallpaperId = wallpaper.Id;
            _settings.Save();

            _log.Info($"Applied wallpaper: {wallpaper.FileName}");
            Refresh();
        }
        catch (Exception ex)
        {
            var msg = LocalizationService.GetString("Wallpapers_CannotPlay", "Wallpaper cannot be played.");
            _log.Error(msg, ex);
            ModernDialogWindow.Show(
                msg,
                "Livy", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            wallpaper.IsApplying = false;
            IsApplyingAny = false;
            IsOptimizing = false;
        }
    }

    public void PreviewWallpaper(Wallpaper? wallpaper)
    {
        if (wallpaper is null || !wallpaper.FileExists) return;

        var activeId = _settings.Settings.ActiveWallpaperId;
        var isCurrentlyActive = (wallpaper.Id == activeId);

        var previewWin = new Views.WallpaperPreviewWindow(wallpaper, isCurrentlyActive, ApplyWallpaper);
        if (System.Windows.Application.Current.MainWindow is not null)
        {
            previewWin.Owner = System.Windows.Application.Current.MainWindow;
        }
        previewWin.ShowDialog();
    }

    public void SetLoopMode(string? mode)
    {
        if (string.IsNullOrEmpty(mode) || ActiveWallpaper is null) return;

        ActiveLoopMode = mode;
        ActiveWallpaper.LoopMode = mode;
        _settings.Settings.DefaultLoopMode = mode;
        _settings.Save();

        _wallpaperManager.ApplyWallpaper(ActiveWallpaper.FilePath, mode);
        Refresh();
    }

    public void EditWallpaper(Wallpaper wallpaper)
    {
        if (wallpaper is null) return;

        var win = new Views.EditWallpaperWindow(wallpaper);
        if (System.Windows.Application.Current.MainWindow is not null)
        {
            win.Owner = System.Windows.Application.Current.MainWindow;
        }

        if (win.ShowDialog() == true)
        {
            _settings.Save();
            
            // Reapply if it's currently active and loop mode might have changed
            if (_settings.Settings.ActiveWallpaperId == wallpaper.Id)
            {
                ActiveLoopMode = wallpaper.LoopMode;
                ApplyWallpaper(wallpaper);
            }
        }
    }

    public void DeleteWallpaper(Wallpaper wallpaper)
    {
        if (wallpaper is null) return;

        if (_settings.Settings.ConfirmBeforeDelete)
        {
            var title = LocalizationService.GetString("Wallpapers_ConfirmDeleteTitle", "Confirm Delete");
            var format = LocalizationService.GetString("Wallpapers_ConfirmDeleteMessage", "Are you sure you want to remove \"{0}\" from the library?");
            var result = ModernDialogWindow.Show(
                string.Format(format, wallpaper.FileName),
                title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                isDestructive: true);

            if (result != MessageBoxResult.Yes) return;
        }

        // If this is the active wallpaper, remove it from desktop
        if (_settings.Settings.ActiveWallpaperId == wallpaper.Id)
        {
            _wallpaperManager.RemoveWallpaper();
            _settings.Settings.ActiveWallpaperId = null;
        }

        // Delete physical file from internal storage and any loop cache
        WallpaperStorageService.DeleteWallpaperFiles(wallpaper);

        _settings.Settings.Wallpapers.Remove(wallpaper);
        Wallpapers.Remove(wallpaper);
        if (SelectedWallpaper == wallpaper)
        {
            SelectedWallpaper = null;
        }

        _settings.Save();
        Refresh();
    }
}
