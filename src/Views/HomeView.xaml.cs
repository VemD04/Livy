using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Livy.ViewModels;

namespace Livy.Views;

public partial class HomeView : UserControl
{
    private HomeViewModel? _currentVm;

    public HomeView()
    {
        InitializeComponent();
        DataContextChanged += HomeView_DataContextChanged;
    }

    private void HomeView_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_currentVm != null)
        {
            _currentVm.PropertyChanged -= Vm_PropertyChanged;
        }

        _currentVm = e.NewValue as HomeViewModel;

        if (_currentVm != null)
        {
            _currentVm.PropertyChanged += Vm_PropertyChanged;
            UpdateActiveMediaPlayback();
        }
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HomeViewModel.ActiveWallpaperVideoPath) ||
            e.PropertyName == nameof(HomeViewModel.ActiveWallpaper) ||
            e.PropertyName == nameof(HomeViewModel.HasActiveWallpaper))
        {
            UpdateActiveMediaPlayback();
        }
        else if (e.PropertyName == nameof(HomeViewModel.IsAutoPaused) || e.PropertyName == nameof(HomeViewModel.IsPaused))
        {
            if (_currentVm != null)
            {
                if (_currentVm.IsPaused || _currentVm.IsAutoPaused)
                {
                    try { ActiveMediaElement.Pause(); } catch { }
                }
                else
                {
                    try { ActiveMediaElement.Play(); } catch { }
                }
            }
        }
    }

    private Window? _parentWindow;

    private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _parentWindow = Window.GetWindow(this);
        if (_parentWindow != null)
        {
            _parentWindow.StateChanged -= ParentWindow_StateChanged;
            _parentWindow.StateChanged += ParentWindow_StateChanged;
            _parentWindow.IsVisibleChanged -= ParentWindow_IsVisibleChanged;
            _parentWindow.IsVisibleChanged += ParentWindow_IsVisibleChanged;
        }

        if (DataContext is HomeViewModel vm)
        {
            if (_currentVm != vm)
            {
                if (_currentVm != null) _currentVm.PropertyChanged -= Vm_PropertyChanged;
                _currentVm = vm;
                _currentVm.PropertyChanged += Vm_PropertyChanged;
            }
            UpdateActiveMediaPlayback();
        }
    }

    private void UserControl_Unloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_parentWindow != null)
        {
            _parentWindow.StateChanged -= ParentWindow_StateChanged;
            _parentWindow.IsVisibleChanged -= ParentWindow_IsVisibleChanged;
            _parentWindow = null;
        }

        try
        {
            ActiveMediaElement.Stop();
            ActiveMediaElement.Close();
            ActiveMediaElement.Source = null;
        }
        catch { }
    }

    private void ParentWindow_StateChanged(object? sender, EventArgs e) => UpdateVisibilityOrStatePlayback();
    private void ParentWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateVisibilityOrStatePlayback();

    private void UpdateVisibilityOrStatePlayback()
    {
        if (_parentWindow == null) return;
        bool isWindowHiddenOrMinimized = !_parentWindow.IsVisible || _parentWindow.WindowState == WindowState.Minimized;
        if (isWindowHiddenOrMinimized)
        {
            try { ActiveMediaElement.Pause(); } catch { }
        }
        else if (_currentVm != null && !_currentVm.IsPaused && !_currentVm.IsAutoPaused)
        {
            try { ActiveMediaElement.Play(); } catch { }
        }
    }

    private void UpdateActiveMediaPlayback()
    {
        if (_currentVm == null) return;

        var videoPath = _currentVm.ActiveWallpaperVideoPath;
        if (_currentVm.HasActiveWallpaper && !string.IsNullOrEmpty(videoPath) && System.IO.File.Exists(videoPath))
        {
            try
            {
                var uri = new Uri(videoPath);
                if (ActiveMediaElement.Source != uri)
                {
                    ActiveMediaElement.Source = uri;
                }
                ActiveMediaElement.Position = TimeSpan.Zero;
                if (!_currentVm.IsAutoPaused && !_currentVm.IsPaused)
                {
                    ActiveMediaElement.Play();
                }
                else
                {
                    ActiveMediaElement.Pause();
                }
                ActiveMediaElement.Visibility = System.Windows.Visibility.Visible;
            }
            catch
            {
                ActiveMediaElement.Visibility = System.Windows.Visibility.Collapsed;
            }
        }
        else
        {
            try
            {
                ActiveMediaElement.Stop();
                ActiveMediaElement.Source = null;
            }
            catch { }
            ActiveMediaElement.Visibility = System.Windows.Visibility.Collapsed;
        }
    }

    private void ActiveMediaElement_MediaOpened(object sender, System.Windows.RoutedEventArgs e)
    {
        ActiveMediaElement.Visibility = System.Windows.Visibility.Visible;
        if (_currentVm != null && !_currentVm.IsPaused && !_currentVm.IsAutoPaused)
        {
            ActiveMediaElement.Play();
        }
    }

    private void ActiveMediaElement_MediaEnded(object sender, System.Windows.RoutedEventArgs e)
    {
        ActiveMediaElement.Position = TimeSpan.Zero;
        ActiveMediaElement.Play();
    }

    private void ActiveMediaElement_MediaFailed(object? sender, System.Windows.ExceptionRoutedEventArgs e)
    {
        ActiveMediaElement.Visibility = System.Windows.Visibility.Collapsed;
    }

    private static Models.Wallpaper? GetWallpaperFromSource(object sender)
    {
        if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu)
        {
            if (contextMenu.PlacementTarget is System.Windows.FrameworkElement target)
            {
                return target.DataContext as Models.Wallpaper;
            }
        }
        return sender is System.Windows.FrameworkElement fe ? fe.DataContext as Models.Wallpaper : null;
    }

    private void PreviewButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel vm) return;
        var wp = GetWallpaperFromSource(sender);
        if (wp is null) return;

        vm.SelectedWallpaper = wp;
        vm.PreviewWallpaperCommand.Execute(null);
        e.Handled = true;
    }

    private void ApplyButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel vm) return;
        var wp = GetWallpaperFromSource(sender);
        if (wp is null) return;

        vm.SelectedWallpaper = wp;
        vm.ApplyWallpaperCommand.Execute(null);
        e.Handled = true;
    }

    private void DeleteButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not HomeViewModel vm) return;
        var wp = GetWallpaperFromSource(sender);
        if (wp is null) return;

        vm.SelectedWallpaper = wp;
        vm.DeleteWallpaperCommand.Execute(null);
        e.Handled = true;
    }

    private void EditWallpaperMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel vm) return;
        var wp = GetWallpaperFromSource(sender);
        if (wp is null) return;

        vm.SelectedWallpaper = wp;
        vm.EditWallpaperCommand.Execute(null);
    }

    private void DeleteWallpaperMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel vm) return;
        var wp = GetWallpaperFromSource(sender);
        if (wp is null) return;

        vm.SelectedWallpaper = wp;
        vm.DeleteWallpaperCommand.Execute(null);
    }

    private void ActiveThumbnail_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is HomeViewModel vm && vm.ActiveWallpaper is not null)
        {
            vm.PreviewWallpaper(vm.ActiveWallpaper);
        }
    }

    private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scv)
        {
            scv.ScrollToVerticalOffset(scv.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }
}
