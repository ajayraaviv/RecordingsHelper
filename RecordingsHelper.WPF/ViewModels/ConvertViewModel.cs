using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Lame;

namespace RecordingsHelper.WPF.ViewModels;

public partial class ConvertViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ConversionItem> _files = new();

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private int _selectedBitrate = 128;

    [ObservableProperty]
    private string _outputFolder = string.Empty;

    [ObservableProperty]
    private bool _deleteOriginal;

    [ObservableProperty]
    private int _progress;

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private int _processedFiles;

    public int[] AvailableBitrates { get; } = { 64, 96, 128, 160, 192, 256, 320 };

    [RelayCommand]
    private void AddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Audio/Video Files (*.wav;*.mp3;*.mp4)|*.wav;*.mp3;*.mp4|All Files (*.*)|*.*",
            Multiselect = true,
            Title = "Select audio or MP4 files to convert"
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                if (!Files.Any(f => f.FilePath == file))
                {
                    var fileInfo = new FileInfo(file);
                    Files.Add(new ConversionItem
                    {
                        FilePath = file,
                        FileName = Path.GetFileName(file),
                        OriginalSize = fileInfo.Length,
                        Status = "Ready"
                    });
                }
            }

            StatusMessage = $"{Files.Count} file(s) ready for conversion";
        }
    }

    [RelayCommand]
    private void RemoveFile(ConversionItem item)
    {
        Files.Remove(item);
        StatusMessage = $"{Files.Count} file(s) ready for conversion";
    }

    [RelayCommand]
    private void ClearAll()
    {
        Files.Clear();
        StatusMessage = string.Empty;
        ProcessedFiles = 0;
        TotalFiles = 0;
        Progress = 0;
    }

    [RelayCommand]
    private void SelectOutputFolder()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select output folder for MP3 files",
            Filter = "Folder Selection|*.",
            FileName = "Select Folder",
            CheckFileExists = false,
            CheckPathExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            OutputFolder = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
        }
    }

    [RelayCommand]
    private async Task ConvertFiles()
    {
        if (Files.Count == 0)
        {
            MessageBox.Show("Please add files to convert.", "No Files", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // For single file, use SaveFileDialog
        if (Files.Count == 1)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "MP3 Files (*.mp3)|*.mp3",
                FileName = Path.GetFileNameWithoutExtension(Files[0].FileName) + ".mp3",
                Title = "Save MP3 File"
            };

            if (saveDialog.ShowDialog() != true)
                return;

            OutputFolder = Path.GetDirectoryName(saveDialog.FileName) ?? string.Empty;
        }
        else
        {
            // For multiple files, require output folder
            if (string.IsNullOrWhiteSpace(OutputFolder))
            {
                MessageBox.Show("Please select an output folder.", "No Output Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(OutputFolder))
            {
                MessageBox.Show("Output folder does not exist.", "Invalid Folder", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        IsProcessing = true;
        TotalFiles = Files.Count;
        ProcessedFiles = 0;
        Progress = 0;

        try
        {
            foreach (var item in Files)
            {
                StatusMessage = $"Converting {item.FileName}...";
                item.Status = "Converting...";

                try
                {
                    await Task.Run(() => ConvertWavToMp3(item.FilePath, OutputFolder, SelectedBitrate));

                    var outputFilePath = Path.Combine(OutputFolder, Path.GetFileNameWithoutExtension(item.FileName) + ".mp3");
                    var outputFileInfo = new FileInfo(outputFilePath);
                    item.ConvertedSize = outputFileInfo.Length;
                    item.Status = "Completed";
                    item.SavingsPercent = ((item.OriginalSize - item.ConvertedSize) / (double)item.OriginalSize) * 100;

                    if (DeleteOriginal && File.Exists(item.FilePath))
                    {
                        File.Delete(item.FilePath);
                    }
                }
                catch (Exception ex)
                {
                    item.Status = $"Error: {ex.Message}";
                }

                ProcessedFiles++;
                Progress = (int)((ProcessedFiles / (double)TotalFiles) * 100);
            }

            StatusMessage = $"Conversion complete! {ProcessedFiles} of {TotalFiles} file(s) converted successfully.";
            MessageBox.Show($"Conversion complete!\n\nOutput folder: {OutputFolder}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error during conversion: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private void ConvertWavToMp3(string inputPath, string outputFolder, int bitrate)
    {
        var outputFileName = Path.GetFileNameWithoutExtension(inputPath) + ".mp3";
        var outputPath = Path.Combine(outputFolder, outputFileName);

        // Handle file name conflicts
        int counter = 1;
        while (File.Exists(outputPath))
        {
            outputFileName = Path.GetFileNameWithoutExtension(inputPath) + $"-{counter}.mp3";
            outputPath = Path.Combine(outputFolder, outputFileName);
            counter++;
        }

        using var reader = new AudioFileReader(inputPath);
        using var writer = new LameMP3FileWriter(outputPath, reader.WaveFormat, bitrate);
        reader.CopyTo(writer);
    }

    public void Cleanup()
    {
        // Reset state
        Files.Clear();
        IsProcessing = false;
        StatusMessage = string.Empty;
        Progress = 0;
        TotalFiles = 0;
        ProcessedFiles = 0;
        OutputFolder = string.Empty;
    }
}

public partial class ConversionItem : ObservableObject
{
    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private long _originalSize;

    [ObservableProperty]
    private long _convertedSize;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private double _savingsPercent;

    public string OriginalSizeFormatted => FormatFileSize(OriginalSize);
    public string ConvertedSizeFormatted => ConvertedSize > 0 ? FormatFileSize(ConvertedSize) : "-";
    public string SavingsFormatted => SavingsPercent > 0 ? $"{SavingsPercent:F1}%" : "-";

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    partial void OnOriginalSizeChanged(long value)
    {
        OnPropertyChanged(nameof(OriginalSizeFormatted));
    }

    partial void OnConvertedSizeChanged(long value)
    {
        OnPropertyChanged(nameof(ConvertedSizeFormatted));
        OnPropertyChanged(nameof(SavingsFormatted));
    }

    partial void OnSavingsPercentChanged(double value)
    {
        OnPropertyChanged(nameof(SavingsFormatted));
    }
}
