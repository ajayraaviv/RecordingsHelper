using System.Windows;

namespace RecordingsHelper.WPF.Views;

public partial class ProgressDialog : Window
{
    public ProgressDialog()
    {
        InitializeComponent();
    }

    public void UpdateMessage(string message)
    {
        Dispatcher.Invoke(() => MessageTextBlock.Text = message);
    }
}
