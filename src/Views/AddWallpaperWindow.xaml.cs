using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Livy.Models;
using Livy.Services;

namespace Livy.Views;

public partial class AddWallpaperWindow : Window
{
    private static readonly string[] SupportedExtensions = [".mp4", ".webm", ".mkv", ".avi", ".mov"];

    public List<Wallpaper> AddedWallpapers { get; } = [];

    public AddWallpaperWindow()
    {
        InitializeComponent();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (ProcessingOverlay.Visibility == Visibility.Visible) return;
        DialogResult = AddedWallpapers.Count > 0;
        Close();
    }

    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            SetDragOverState(true);
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        SetDragOverState(false);
    }

    private async void DropZone_Drop(object sender, DragEventArgs e)
    {
        SetDragOverState(false);

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                await ProcessFilesAsync(files);
            }
        }
    }

    private async void DropZone_Click(object sender, MouseButtonEventArgs e)
    {
        // Only trigger browse if clicking left button and not currently processing
        if (e.ChangedButton == MouseButton.Left && ProcessingOverlay.Visibility != Visibility.Visible)
        {
            await BrowseAndProcessAsync();
        }
    }

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        if (ProcessingOverlay.Visibility != Visibility.Visible)
        {
            await BrowseAndProcessAsync();
        }
    }

    private async Task BrowseAndProcessAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = LocalizationService.GetString("AddDialog_Title", "Select Video File"),
            Filter = "Video Files|*.mp4;*.webm;*.mkv;*.avi;*.mov|MP4 Files|*.mp4|WebM Files|*.webm|MKV Files|*.mkv|All Files|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            await ProcessFilesAsync(dialog.FileNames);
        }
    }

    private void SetDragOverState(bool isOver)
    {
        if (isOver)
        {
            if (FindResource("AccentBrush") is Brush accentBrush)
            {
                DropZoneRect.Stroke = accentBrush;
            }
            DropZoneRect.StrokeThickness = 3;
            PromptTitleText.Text = LocalizationService.GetString("AddDialog_DropNow", "Drop video file here to import");
        }
        else
        {
            if (FindResource("BorderBrush") is Brush borderBrush)
            {
                DropZoneRect.Stroke = borderBrush;
            }
            DropZoneRect.StrokeThickness = 2;
            PromptTitleText.Text = LocalizationService.GetString("AddDialog_DragDropPrompt", "Drag and drop video files here");
        }
    }

    private async Task ProcessFilesAsync(IEnumerable<string> filePaths)
    {
        var validFiles = filePaths
            .Where(f => !string.IsNullOrWhiteSpace(f) && File.Exists(f))
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        if (validFiles.Count == 0)
        {
            var msg = LocalizationService.GetString("AddDialog_NoValidFiles", "Please select supported video files (.mp4, .webm, .mkv, .avi, .mov).");
            ModernDialogWindow.Show(this, msg, "Livy", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ProcessingOverlay.Visibility = Visibility.Visible;
        NormalContent.Visibility = Visibility.Collapsed;
        CloseButton.IsEnabled = false;
        BrowseButton.IsEnabled = false;

        var addedList = new List<Wallpaper>();

        await Task.Run(() =>
        {
            var settings = SettingsService.Instance;
            for (int i = 0; i < validFiles.Count; i++)
            {
                var filePath = validFiles[i];
                try
                {
                    var fileDisplayName = Path.GetFileNameWithoutExtension(filePath);
                    Dispatcher.Invoke(() =>
                    {
                        ProcessingDetailText.Text = validFiles.Count > 1
                            ? $"({i + 1}/{validFiles.Count}) {fileDisplayName}"
                            : fileDisplayName;
                    });

                    var ext = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();

                    var newId = Guid.NewGuid().ToString("N");
                    var storedPath = WallpaperStorageService.ImportWallpaperFile(filePath, newId);

                    // Extract video metadata
                    var (width, height, duration, fps) = VideoPlayerService.GetVideoInfo(storedPath);

                    var wallpaper = new Wallpaper
                    {
                        Id = newId,
                        FilePath = storedPath,
                        FileName = fileDisplayName,
                        Extension = ext,
                        Width = width,
                        Height = height,
                        Duration = duration,
                        Fps = fps
                    };

                    settings.Settings.Wallpapers.Add(wallpaper);
                    addedList.Add(wallpaper);

                    // Preload thumbnail in background
                    wallpaper.LoadThumbnailAsync();

                    // Pre-generate seamless loop in background so it's instantly ready when user applies it
                    if (SeamlessLoopService.Instance.IsFfmpegAvailable)
                    {
                        var defaultMode = settings.Settings.DefaultLoopMode ?? "PingPong";
                        if (defaultMode != "Standard")
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await SeamlessLoopService.Instance.GetOrGenerateLoopVideoAsync(storedPath, defaultMode, duration);
                                }
                                catch { }
                            });
                        }
                    }

                    LogService.Instance.Info($"Added wallpaper via popup: {fileDisplayName}");
                }
                catch (Exception ex)
                {
                    LogService.Instance.Error($"Failed to process wallpaper file: {filePath}", ex);
                }
            }

            settings.Save();
        });

        CloseButton.IsEnabled = true;
        BrowseButton.IsEnabled = true;

        AddedWallpapers.AddRange(addedList);
        DialogResult = AddedWallpapers.Count > 0;
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            if (ProcessingOverlay.Visibility == Visibility.Visible) return;
            DialogResult = AddedWallpapers.Count > 0;
            Close();
        }
    }
}
