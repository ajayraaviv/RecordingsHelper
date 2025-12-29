using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace RecordingsHelper.WPF.Views
{
    public partial class BlobUrlInputDialog : Window
    {
        public List<string> BlobUrls { get; private set; } = new List<string>();

        public BlobUrlInputDialog()
        {
            InitializeComponent();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var text = BlobUrlsTextBox.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("Please enter at least one blob URL.", "No URLs", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(line => line.Trim())
                           .Where(line => !string.IsNullOrWhiteSpace(line))
                           .ToList();

            if (lines.Count == 0)
            {
                MessageBox.Show("Please enter at least one valid blob URL.", "No Valid URLs", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate URLs
            var invalidUrls = new List<string>();
            var validUrls = new List<string>();

            foreach (var line in lines)
            {
                if (Uri.TryCreate(line, UriKind.Absolute, out var uri) && 
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    validUrls.Add(line);
                }
                else
                {
                    invalidUrls.Add(line);
                }
            }

            if (invalidUrls.Count > 0)
            {
                var result = MessageBox.Show(
                    $"Found {invalidUrls.Count} invalid URL(s).\n\nDo you want to add the {validUrls.Count} valid URL(s)?",
                    "Invalid URLs Detected",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            if (validUrls.Count == 0)
            {
                MessageBox.Show("No valid URLs found. Please check the format.", "No Valid URLs", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            BlobUrls = validUrls;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
