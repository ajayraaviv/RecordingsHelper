using System.Windows;
using RecordingsHelper.WPF.ViewModels;

namespace RecordingsHelper.WPF;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}