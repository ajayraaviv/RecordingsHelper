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

    public MainViewModel()
    {
        MergeViewModel = new MergeViewModel();
        RedactViewModel = new RedactViewModel();
        SplitViewModel = new SplitViewModel();
        AdvancedRedactViewModel = new AdvancedRedactViewModel();
        ConvertViewModel = new ConvertViewModel();
        TranscriptionViewModel = new TranscriptionViewModel();
        BatchTranscriptionViewModel = new BatchTranscriptionViewModel();
        CurrentView = null; // Home page by default
    }

    [RelayCommand]
    private void NavigateToMerge()
    {
        CurrentView = MergeViewModel;
    }

    [RelayCommand]
    private void NavigateToRedact()
    {
        CurrentView = RedactViewModel;
    }

    [RelayCommand]
    private void NavigateToSplit()
    {
        CurrentView = SplitViewModel;
    }

    [RelayCommand]
    private void NavigateToAdvancedRedact()
    {
        CurrentView = AdvancedRedactViewModel;
    }

    [RelayCommand]
    private void NavigateToConvert()
    {
        CurrentView = ConvertViewModel;
    }

    [RelayCommand]
    private void NavigateToTranscription()
    {
        CurrentView = TranscriptionViewModel;
    }

    [RelayCommand]
    private void NavigateToBatchTranscription()
    {
        CurrentView = BatchTranscriptionViewModel;
    }

    [RelayCommand]
    private void NavigateToHome()
    {
        CurrentView = null;
    }
}
