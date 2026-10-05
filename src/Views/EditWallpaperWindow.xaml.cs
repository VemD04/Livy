using System.IO;
using System.Windows;
using System.Windows.Input;
using Livy.Models;

namespace Livy.Views;

public partial class EditWallpaperWindow : Window
{
    private readonly Wallpaper _wallpaper;

    public EditWallpaperWindow(Wallpaper wallpaper)
    {
        InitializeComponent();
        _wallpaper = wallpaper;
        
        FileNameInput.Text = _wallpaper.FileName;

        if (_wallpaper.LoopMode == "PingPong")
            RadioPingPong.IsChecked = true;
        else if (_wallpaper.LoopMode == "Seamless")
            RadioSeamless.IsChecked = true;
        else
            RadioStandard.IsChecked = true;

        Loaded += (s, e) =>
        {
            FileNameInput.Focus();
            FileNameInput.SelectAll();
        };
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            this.DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var cleanName = string.IsNullOrWhiteSpace(FileNameInput.Text) ? "Wallpaper" : FileNameInput.Text.Trim();
        _wallpaper.FileName = Path.GetFileNameWithoutExtension(cleanName);

        if (RadioPingPong.IsChecked == true)
            _wallpaper.LoopMode = "PingPong";
        else if (RadioSeamless.IsChecked == true)
            _wallpaper.LoopMode = "Seamless";
        else
            _wallpaper.LoopMode = "Standard";

        DialogResult = true;
        Close();
    }
}
