using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Livy.Services;

namespace Livy.Views;

public partial class ModernDialogWindow : Window
{
    private readonly MessageBoxButton _buttonType;
    private readonly bool _isDestructive;

    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    public ModernDialogWindow(
        string message,
        string title = "Livy",
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None,
        bool isDestructive = false)
    {
        InitializeComponent();

        _buttonType = button;
        _isDestructive = isDestructive;

        Title = title;
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;

        ConfigureAppearance(icon, button);
    }

    private void ConfigureAppearance(MessageBoxImage icon, MessageBoxButton button)
    {
        // 1. Icon & Accent Styling
        switch (icon)
        {
            case MessageBoxImage.Question:
                if (_isDestructive)
                {
                    IconBadge.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xEF, 0x44, 0x44));
                    IconPath.Data = (Geometry)FindResource("DeleteIcon");
                    IconPath.Fill = (Brush)FindResource("DangerBrush");
                }
                else
                {
                    IconBadge.Background = new SolidColorBrush(Color.FromArgb(0x33, 0x3C, 0x7B, 0xFB));
                    IconPath.Data = (Geometry)FindResource("QuestionIcon");
                    IconPath.Fill = (Brush)FindResource("AccentBrush");
                }
                break;

            case MessageBoxImage.Warning:
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xF5, 0x9E, 0x0B));
                IconPath.Data = (Geometry)FindResource("WarningIcon");
                IconPath.Fill = (Brush)FindResource("WarningBrush");
                break;

            case MessageBoxImage.Error:
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xEF, 0x44, 0x44));
                IconPath.Data = (Geometry)FindResource("ErrorIcon");
                IconPath.Fill = (Brush)FindResource("DangerBrush");
                break;

            case MessageBoxImage.Information:
            default:
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(0x33, 0x3C, 0x7B, 0xFB));
                IconPath.Data = (Geometry)FindResource("AboutIcon");
                IconPath.Fill = (Brush)FindResource("AccentBrush");
                break;
        }

        // 2. Buttons Styling
        switch (button)
        {
            case MessageBoxButton.OK:
                SecondaryButton.Visibility = Visibility.Collapsed;
                PrimaryButton.Content = LocalizationService.GetString("Dialog_OK", "OK");
                PrimaryButton.Style = (Style)FindResource("PrimaryButton");
                break;

            case MessageBoxButton.YesNo:
                SecondaryButton.Visibility = Visibility.Visible;
                SecondaryButton.Content = LocalizationService.GetString("Dialog_No", "Tidak");

                if (_isDestructive)
                {
                    PrimaryButton.Content = LocalizationService.GetString("Dialog_DeleteConfirm", "Hapus");
                    PrimaryButton.Style = (Style)FindResource("DangerSolidButton");
                }
                else
                {
                    PrimaryButton.Content = LocalizationService.GetString("Dialog_Yes", "Ya");
                    PrimaryButton.Style = (Style)FindResource("PrimaryButton");
                }
                break;

            case MessageBoxButton.OKCancel:
                SecondaryButton.Visibility = Visibility.Visible;
                SecondaryButton.Content = LocalizationService.GetString("Dialog_Cancel", "Batal");
                PrimaryButton.Content = LocalizationService.GetString("Dialog_OK", "OK");
                PrimaryButton.Style = (Style)FindResource("PrimaryButton");
                break;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Result = _buttonType switch
        {
            MessageBoxButton.YesNo => MessageBoxResult.No,
            _ => MessageBoxResult.Cancel
        };
        Close();
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        Result = _buttonType switch
        {
            MessageBoxButton.YesNo => MessageBoxResult.Yes,
            _ => MessageBoxResult.OK
        };
        DialogResult = true;
        Close();
    }

    private void SecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        Result = _buttonType switch
        {
            MessageBoxButton.YesNo => MessageBoxResult.No,
            _ => MessageBoxResult.Cancel
        };
        DialogResult = false;
        Close();
    }

    /// <summary>
    /// Shows a modern styled alert/confirmation dialog.
    /// </summary>
    public static MessageBoxResult Show(
        string message,
        string title = "Livy",
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None,
        Window? owner = null,
        bool isDestructive = false)
    {
        var runOnDispatcher = Application.Current?.Dispatcher;
        if (runOnDispatcher != null && !runOnDispatcher.CheckAccess())
        {
            return runOnDispatcher.Invoke(() => Show(message, title, button, icon, owner, isDestructive));
        }

        var dialog = new ModernDialogWindow(message, title, button, icon, isDestructive);

        // Find active window or main window to center upon
        owner ??= Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible)
                  ?? Application.Current?.MainWindow;

        if (owner != null && owner.IsVisible)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        dialog.ShowDialog();
        return dialog.Result;
    }

    /// <summary>
    /// Shows a modern styled alert with specific window owner.
    /// </summary>
    public static MessageBoxResult Show(
        Window owner,
        string message,
        string title = "Livy",
        MessageBoxButton button = MessageBoxButton.OK,
        MessageBoxImage icon = MessageBoxImage.None,
        bool isDestructive = false)
    {
        return Show(message, title, button, icon, owner, isDestructive);
    }
}
