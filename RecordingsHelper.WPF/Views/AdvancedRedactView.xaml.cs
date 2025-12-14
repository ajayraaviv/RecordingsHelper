
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

    // Toggle IsSelected when the card is clicked (except when clicking the checkbox or toggle)
    private void SegmentCard_Click(object sender, MouseButtonEventArgs e)
    {
        var fe = e.OriginalSource as FrameworkElement;
        if (fe == null) return;
        if (fe.DataContext is not RecordingsHelper.WPF.ViewModels.TranscriptItemViewModel vm) return;
        // Avoid toggling if click was on CheckBox or ToggleButton
        if (fe is CheckBox || fe is ToggleButton) return;
        vm.IsSelected = !vm.IsSelected;
    }
}
