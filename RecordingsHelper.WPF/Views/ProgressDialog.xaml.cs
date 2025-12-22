using System.Threading;
using System.Windows;

namespace RecordingsHelper.WPF.Views;

public partial class ProgressDialog : Window
{
    private CancellationTokenSource? _cancellationTokenSource;

    public bool ShowCancelButton
    {
        get => CancelButton.Visibility == Visibility.Visible;
        set
        {
            CancelButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            CancelHintText.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public ProgressDialog()
    {
        InitializeComponent();
        ShowCancelButton = false; // Default to hidden
    }

    public void SetCancellationTokenSource(CancellationTokenSource cancellationTokenSource)
    {
        _cancellationTokenSource = cancellationTokenSource;
    }

    public void UpdateMessage(string message)
    {
        Dispatcher.Invoke(() => MessageTextBlock.Text = message);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        CancelButton.IsEnabled = false;
        UpdateMessage("Canceling transcription...");
    }
}
