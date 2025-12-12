using System.Windows.Controls;

namespace RecordingsHelper.WPF.Views;

public partial class RedactView : UserControl
{
    public RedactView()
    {
        InitializeComponent();
    }

    private void TimeTextBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            // Select all text when the textbox receives focus
            textBox.SelectAll();
        }
    }
}
