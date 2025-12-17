
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace RecordingsHelper.WPF.Views;

public partial class AdvancedRedactView : UserControl
{
    public AdvancedRedactView()
    {
        InitializeComponent();
    }

    // Toggle IsSelected when transcript text is clicked
    private void TranscriptText_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is RecordingsHelper.WPF.ViewModels.TranscriptItemViewModel vm)
        {
            vm.IsSelected = !vm.IsSelected;
        }
    }
}
