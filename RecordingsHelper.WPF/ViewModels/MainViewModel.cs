using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RecordingsHelper.WPF.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private object? _currentView;

    public MergeViewModel MergeViewModel { get; }
    public RedactViewModel RedactViewModel { get; }
    public SplitViewModel SplitViewModel { get; }
    public AdvancedRedactViewModel AdvancedRedactViewModel { get; }
    public ConvertViewModel ConvertViewModel { get; }
    public TranscriptionViewModel TranscriptionViewModel { get; }
    public BatchTranscriptionViewModel BatchTranscriptionViewModel { get; }
    public TranscriptComparisonViewModel TranscriptComparisonViewModel { get; }

    public MainViewModel()
    {
        MergeViewModel = new MergeViewModel();
        RedactViewModel = new RedactViewModel();
        SplitViewModel = new SplitViewModel();
        AdvancedRedactViewModel = new AdvancedRedactViewModel();
        ConvertViewModel = new ConvertViewModel();
        TranscriptionViewModel = new TranscriptionViewModel();
        BatchTranscriptionViewModel = new BatchTranscriptionViewModel();
        TranscriptComparisonViewModel = new TranscriptComparisonViewModel();
        CurrentView = null; // Home page by default
    }

    [RelayCommand]
    private void NavigateToMerge()
    {
        CleanupCurrentView();
        CurrentView = MergeViewModel;
    }

    [RelayCommand]
    private void NavigateToRedact()
    {
        CleanupCurrentView();
        CurrentView = RedactViewModel;
    }

    [RelayCommand]
    private void NavigateToSplit()
    {
        CleanupCurrentView();
        CurrentView = SplitViewModel;
    }

    [RelayCommand]
    private void NavigateToAdvancedRedact()
    {
        CleanupCurrentView();
        CurrentView = AdvancedRedactViewModel;
    }

    [RelayCommand]
    private void NavigateToConvert()
    {
        CleanupCurrentView();
        CurrentView = ConvertViewModel;
    }

    [RelayCommand]
    private void NavigateToTranscription()
    {
        CleanupCurrentView();
        CurrentView = TranscriptionViewModel;
    }

    [RelayCommand]
    private void NavigateToBatchTranscription()
    {
        CleanupCurrentView();
        CurrentView = BatchTranscriptionViewModel;
    }

    [RelayCommand]
    private void NavigateToTranscriptComparison()
    {
        CleanupCurrentView();
        CurrentView = TranscriptComparisonViewModel;
    }

    [RelayCommand]
    private void NavigateToHome()
    {
        CleanupCurrentView();
        CurrentView = null;
    }

    private void CleanupCurrentView()
    {
        // Cleanup based on current view
        if (CurrentView == TranscriptionViewModel)
        {
            TranscriptionViewModel.Cleanup();
        }
        else if (CurrentView == BatchTranscriptionViewModel)
        {
            BatchTranscriptionViewModel.Cleanup();
        }
        else if (CurrentView == TranscriptComparisonViewModel)
        {
            TranscriptComparisonViewModel.Cleanup();
        }
        else if (CurrentView == RedactViewModel)
        {
            RedactViewModel.Cleanup();
        }
        else if (CurrentView == SplitViewModel)
        {
            SplitViewModel.Cleanup();
        }
        else if (CurrentView == MergeViewModel)
        {
            MergeViewModel.Cleanup();
        }
        else if (CurrentView == ConvertViewModel)
        {
            ConvertViewModel.Cleanup();
        }
        else if (CurrentView == AdvancedRedactViewModel)
        {
            AdvancedRedactViewModel.Cleanup();
        }
    }
}
