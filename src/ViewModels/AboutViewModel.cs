using System.Diagnostics;
using System.Windows;
using Livy.Helpers;
using Livy.Views;

namespace Livy.ViewModels;

/// <summary>
/// ViewModel for the About and Documentation page.
/// </summary>
public class AboutViewModel : ObservableObject
{
    public string AppName { get; } = "Livy";
    public string Tagline { get; } = "Bring Your Desktop to Life.";
    public string Version { get; } = "v1.0.0";
    public string DeveloperName { get; } = "VemD04";
    public string GitHubUrl { get; } = "https://github.com/VemD04";

    public string QrisMerchant { get; } = "SuppDEV";
    public string QrisNmid { get; } = "ID1026608978510";

    public RelayCommand OpenGitHubCommand { get; }
    public RelayCommand CopyNmidCommand { get; }
    public RelayCommand ShowSupportCommand { get; }

    public AboutViewModel()
    {
        ShowSupportCommand = new RelayCommand(() =>
        {
            try
            {
                var win = new SupportDeveloperWindow
                {
                    Owner = Application.Current?.MainWindow
                };
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                ModernDialogWindow.Show("Gagal membuka jendela dukungan: " + ex.Message, "Livy", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
        OpenGitHubCommand = new RelayCommand(() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = GitHubUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ModernDialogWindow.Show(
                    "Gagal membuka tautan GitHub: " + ex.Message,
                    "Livy",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        });

        CopyNmidCommand = new RelayCommand(() =>
        {
            try
            {
                Clipboard.SetText(QrisNmid);
                var title = Services.LocalizationService.GetString("About_SupportTitle", "Dukung Pengembang");
                var msg = Services.LocalizationService.GetString("About_SupportCopied", "NMID berhasil disalin ke clipboard!");
                ModernDialogWindow.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ModernDialogWindow.Show("Gagal menyalin: " + ex.Message, "Livy", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
    }
}

