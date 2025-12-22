using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using RecordingsHelper.Core.Models;

namespace RecordingsHelper.WPF.Views;

public partial class TranscriptionSettingsDialog : Window
{
    public TranscriptionSettingsDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is TranscriptionSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.SubscriptionKey))
            {
                SubscriptionKeyBox.Password = settings.SubscriptionKey;
            }
            
            if (!string.IsNullOrEmpty(settings.StorageAccountKey))
            {
                StorageKeyPasswordBox.Password = settings.StorageAccountKey;
            }
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is TranscriptionSettings settings)
        {
            settings.SubscriptionKey = SubscriptionKeyBox.Password;
            settings.StorageAccountKey = StorageKeyPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(settings.SubscriptionKey))
            {
                MessageBox.Show("Please enter a subscription key.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.Region))
            {
                MessageBox.Show("Please enter a region.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
