using System.Windows;

namespace RecordingsHelper.WPF.Views
{
    public partial class LlmPromptDialog : Window
    {
        public string PromptText { get; private set; } = string.Empty;

        private const string DefaultPrompt = @"This is a Social Security Administration disability hearing conducted by the Office of Hearings Operations.
The hearing is presided over by an Administrative Law Judge and follows formal SSA adjudication procedures.
The Administrative Law Judge asks structured legal questions, provides instructions, and issues rulings on the record.
The claimant provides testimony regarding impairments, symptoms, work history, and functional limitations and may speak informally or with hesitation.
A vocational expert provides testimony regarding past relevant work, residual functional capacity, exertional levels, skill levels, SVP ratings, and DOT job classifications.
A representative or attorney may be present and speaks using formal legal advocacy language.
A medical expert may testify using clinical terminology related to diagnoses, impairments, and functional assessments.
A hearing reporter may announce procedural statements and manage the official hearing record.
An interpreter or translator may repeat or translate statements between languages as part of the hearing.
Legal, medical, and vocational terminology should be transcribed precisely using standard SSA terminology.
Questions and answers should remain distinct and clearly punctuated.
Statements should use proper grammar and punctuation without altering the original meaning or intent.
Pauses, partial sentences, clarifications, and corrections may occur and should be preserved as spoken.";

        public LlmPromptDialog(string? initialPrompt = null)
        {
            InitializeComponent();
            
            // Always use the provided prompt, even if empty
            // Only use default if null (first time)
            if (initialPrompt != null)
            {
                PromptTextBox.Text = initialPrompt;
            }
            else
            {
                PromptTextBox.Text = DefaultPrompt;
            }
            
            PromptTextBox.SelectAll();
            PromptTextBox.Focus();
        }

        private void ApplyDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            PromptTextBox.Text = DefaultPrompt;
            PromptTextBox.SelectAll();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            PromptText = PromptTextBox.Text?.Trim() ?? string.Empty;
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
