using System.Windows;

namespace RecordingsHelper.WPF.Views;

public partial class TranscriptViewerDialog : Window
{
    public TranscriptViewerDialog(string fileName, string transcript)
    {
        InitializeComponent();
        
        HeaderTextBlock.Text = $"Transcript: {fileName}";
        TranscriptTextBox.Text = transcript;
    }

    private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(TranscriptTextBox.Text);
        MessageBox.Show("Transcript copied to clipboard.", "Success", 
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
