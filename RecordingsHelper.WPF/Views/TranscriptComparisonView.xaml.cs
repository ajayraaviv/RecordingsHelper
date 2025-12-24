using System.Windows;
using System.Windows.Controls;
using RecordingsHelper.WPF.ViewModels;

namespace RecordingsHelper.WPF.Views;

public partial class TranscriptComparisonView : UserControl
{
    public TranscriptComparisonView()
    {
        InitializeComponent();
        
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is TranscriptComparisonViewModel viewModel)
        {
            viewModel.LeftPanel.PropertyChanged += OnLeftPanelPropertyChanged;
            viewModel.RightPanel.PropertyChanged += OnRightPanelPropertyChanged;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is TranscriptComparisonViewModel viewModel)
        {
            viewModel.LeftPanel.PropertyChanged -= OnLeftPanelPropertyChanged;
            viewModel.RightPanel.PropertyChanged -= OnRightPanelPropertyChanged;
        }
    }

    private void OnLeftPanelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "IsCollapsed")
        {
            UpdateColumnWidths();
        }
    }

    private void OnRightPanelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "IsCollapsed")
        {
            UpdateColumnWidths();
        }
    }

    private void UpdateColumnWidths()
    {
        if (DataContext is TranscriptComparisonViewModel viewModel)
        {
            var leftColumn = TranscriptGrid.ColumnDefinitions[0];
            var rightColumn = TranscriptGrid.ColumnDefinitions[2];

            if (viewModel.LeftPanel.IsCollapsed)
            {
                leftColumn.Width = new GridLength(50);
            }
            else
            {
                leftColumn.Width = new GridLength(1, GridUnitType.Star);
            }

            if (viewModel.RightPanel.IsCollapsed)
            {
                rightColumn.Width = new GridLength(50);
            }
            else
            {
                rightColumn.Width = new GridLength(1, GridUnitType.Star);
            }
        }
    }
}
