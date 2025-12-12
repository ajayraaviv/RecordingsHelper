using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RecordingsHelper.WPF.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private object? _currentView;

    public MergeViewModel MergeViewModel { get; }
    public RedactViewModel RedactViewModel { get; }

    public MainViewModel()
    {
        MergeViewModel = new MergeViewModel();
        RedactViewModel = new RedactViewModel();
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
    private void NavigateToHome()
    {
        CurrentView = null;
    }
}
