using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RecordingsHelper.Core.Models;
using RecordingsHelper.WPF.ViewModels;

namespace RecordingsHelper.WPF.Views;

public partial class TranscriptionView : UserControl
{
    private Border? _previousActiveSegmentBorder;

    public TranscriptionView()
    {
        InitializeComponent();
        
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is TranscriptionViewModel viewModel)
        {
            viewModel.ScrollToActiveSegment = ScrollToSegment;
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TranscriptionViewModel.ActiveSegment))
        {
            UpdateActiveSegmentHighlight();
        }
    }

    private void UpdateActiveSegmentHighlight()
    {
        var viewModel = DataContext as TranscriptionViewModel;
        if (viewModel == null) return;

        // Clear previous highlight
        if (_previousActiveSegmentBorder != null)
        {
            _previousActiveSegmentBorder.Background = Brushes.Transparent;
        }

        // Highlight new active segment
        if (viewModel.ActiveSegment != null)
        {
            var itemsControl = FindVisualChild<ItemsControl>(TranscriptScrollViewer);
            if (itemsControl != null)
            {
                var index = viewModel.TranscriptionSegments.IndexOf(viewModel.ActiveSegment);
                if (index >= 0)
                {
                    var container = itemsControl.ItemContainerGenerator.ContainerFromIndex(index) as ContentPresenter;
                    if (container != null)
                    {
                        var border = FindVisualChild<Border>(container);
                        if (border != null)
                        {
                            var gradient = new LinearGradientBrush
                            {
                                StartPoint = new Point(0, 0),
                                EndPoint = new Point(1, 0)
                            };
                            gradient.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#90CAF9"), 0));
                            gradient.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
                            
                            border.Background = gradient;
                            _previousActiveSegmentBorder = border;
                        }
                    }
                }
            }
        }
    }

    private void ScrollToSegment(TranscriptionSegment segment)
    {
        Dispatcher.InvokeAsync(() =>
        {
            // Find the container for the segment
            var itemsControl = FindVisualChild<ItemsControl>(TranscriptScrollViewer);
            if (itemsControl == null) return;

            var index = viewModel?.TranscriptionSegments.IndexOf(segment) ?? -1;
            if (index < 0) return;

            // Scroll the segment into view
            var container = itemsControl.ItemContainerGenerator.ContainerFromIndex(index) as FrameworkElement;
            container?.BringIntoView();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private TranscriptionViewModel? viewModel => DataContext as TranscriptionViewModel;

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;

            var result = FindVisualChild<T>(child);
            if (result != null)
                return result;
        }
        return null;
    }
}
