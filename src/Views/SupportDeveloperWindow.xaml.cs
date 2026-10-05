using System.Windows;
using System.Windows.Input;

namespace Livy.Views;

public partial class SupportDeveloperWindow : Window
{
    public SupportDeveloperWindow()
    {
        InitializeComponent();
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
        Close();
    }
}
