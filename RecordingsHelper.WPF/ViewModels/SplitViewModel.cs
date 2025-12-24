using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Lame;
using RecordingsHelper.Core.Services;
using RecordingsHelper.WPF.Services;
using RecordingsHelper.WPF.Views;

namespace RecordingsHelper.WPF.ViewModels;

public partial class SplitViewModel : ObservableObject
{
    private readonly AudioPlayerService _audioPlayer;
    private readonly AudioEditor _audioEditor;
    private readonly DispatcherTimer _positionTimer;

    [ObservableProperty]
    private string? _loadedFilePath;

    [ObservableProperty]
    private string _loadedFileName = string.Empty;

    [ObservableProperty]
    private bool _isFileLoaded;

    [ObservableProperty]
    private TimeSpan _totalDuration;

    [ObservableProperty]
    private TimeSpan _currentPosition;

    [ObservableProperty]
    private double _sliderPosition;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private ObservableCollection<TimeSpan> _splitPoints = new();

    [ObservableProperty]
    private string _newSplitPointText = "00:00.000";

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _outputPattern = "part_{0}.wav";

    [ObservableProperty]
    private bool _compressToMp3;

    [ObservableProperty]
    private int _mp3Bitrate = 128;

    public int[] AvailableBitrates { get; } = { 64, 96, 128, 160, 192, 256, 320 };

    public SplitViewModel()
    {
        _audioPlayer = new AudioPlayerService();
        _audioEditor = new AudioEditor();

        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _positionTimer.Tick += (s, e) => CurrentPosition = _audioPlayer.Position;

        _audioPlayer.PlaybackStopped += (s, e) =>
        {
            IsPlaying = false;
            IsPaused = false;
            _positionTimer.Stop();
        };
    }

    partial void OnNewSplitPointTextChanged(string value)
    {
        // Just store the text, no parsing needed until user adds the split point
    }

    partial void OnCurrentPositionChanged(TimeSpan value)
    {
        SliderPosition = value.TotalMilliseconds;
        if (Math.Abs((_audioPlayer.Position - value).TotalMilliseconds) > 200)
        {
            _audioPlayer.Position = value;
        }
    }

    partial void OnSliderPositionChanged(double value)
    {
        var newPosition = TimeSpan.FromMilliseconds(value);
        if (Math.Abs((CurrentPosition - newPosition).TotalMilliseconds) > 200)
        {
            CurrentPosition = newPosition;
            _audioPlayer.Position = newPosition;
        }
    }

    [RelayCommand]
    private void LoadFile()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Audio Files|*.wav;*.mp3;*.ogg;*.mp4;*.m4a;*.aac;*.wma;*.aiff|All Files|*.*",
            Title = "Select Audio File to Split"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                _audioPlayer.LoadFile(openFileDialog.FileName);
                LoadedFilePath = openFileDialog.FileName;
                LoadedFileName = Path.GetFileName(openFileDialog.FileName);
                TotalDuration = _audioPlayer.Duration;
                CurrentPosition = TimeSpan.Zero;
                IsFileLoaded = true;
                SplitPoints.Clear();
                StatusMessage = $"Loaded {LoadedFileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void UnloadFile()
    {
        if (IsPlaying || IsPaused)
        {
            Stop();
        }

        ResetState();
    }

    [RelayCommand]
    private void PlayPause()
    {
        if (!IsFileLoaded) return;

        if (IsPlaying)
        {
            _audioPlayer.Pause();
            IsPlaying = false;
            IsPaused = true;
            _positionTimer.Stop();
        }
        else
        {
            _audioPlayer.Play();
            IsPlaying = true;
            IsPaused = false;
            _positionTimer.Start();
        }
    }

    [RelayCommand]
    private void Stop()
    {
        _audioPlayer.Stop();
        CurrentPosition = TimeSpan.Zero;
        IsPlaying = false;
        IsPaused = false;
        _positionTimer.Stop();
    }

    [RelayCommand]
    private void AddSplitPointAtCurrent()
    {
        if (!IsFileLoaded) return;

        if (CurrentPosition <= TimeSpan.Zero || CurrentPosition >= TotalDuration)
        {
            MessageBox.Show("Split point must be within the audio file duration.", "Invalid Split Point", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SplitPoints.Contains(CurrentPosition))
        {
            MessageBox.Show("This split point already exists.", "Duplicate Split Point", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SplitPoints.Add(CurrentPosition);
        SplitPoints = new ObservableCollection<TimeSpan>(SplitPoints.OrderBy(t => t));
        NewSplitPointText = FormatTimeSpan(CurrentPosition);
        StatusMessage = $"Added split point at {FormatTimeSpan(CurrentPosition)}";
    }

    [RelayCommand]
    private void AddSplitPointManual()
    {
        var splitPoint = ParseTimeSpan(NewSplitPointText);

        if (splitPoint <= TimeSpan.Zero || splitPoint >= TotalDuration)
        {
            MessageBox.Show("Split point must be within the audio file duration.", "Invalid Split Point", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SplitPoints.Contains(splitPoint))
        {
            MessageBox.Show("This split point already exists.", "Duplicate Split Point", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SplitPoints.Add(splitPoint);
        SplitPoints = new ObservableCollection<TimeSpan>(SplitPoints.OrderBy(t => t));
        NewSplitPointText = "00:00.000";
        StatusMessage = $"Added split point at {FormatTimeSpan(splitPoint)}";
    }

    [RelayCommand]
    private void RemoveSplitPoint(TimeSpan splitPoint)
    {
        SplitPoints.Remove(splitPoint);
        StatusMessage = $"Removed split point at {FormatTimeSpan(splitPoint)}";
    }

    [RelayCommand]
    private void ClearSplitPoints()
    {
        SplitPoints.Clear();
        StatusMessage = "Cleared all split points";
    }

    [RelayCommand]
    private async Task ProcessSplit()
    {
        if (!IsFileLoaded)
        {
            MessageBox.Show("Please load an audio file first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (SplitPoints.Count == 0)
        {
            MessageBox.Show("Please add at least one split point.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var folderDialog = new OpenFolderDialog
        {
            Title = "Select Output Folder for Split Files"
        };

        if (folderDialog.ShowDialog() != true)
            return;

        // Store file path before releasing handle
        var inputFilePath = LoadedFilePath!;
        var baseFileName = Path.GetFileNameWithoutExtension(inputFilePath);

        // Stop playback and release file handle
        if (IsPlaying || IsPaused)
        {
            Stop();
        }
        _audioPlayer.Stop();

        IsProcessing = true;
        StatusMessage = $"Splitting into {SplitPoints.Count + 1} parts...";

        ProgressDialog? progressDialog = null;

        try
        {
            // Show progress dialog
            progressDialog = new ProgressDialog { Title = "Splitting Audio File", Owner = Application.Current.MainWindow };
            
            var dialogTask = Task.Run(() => Application.Current.Dispatcher.Invoke(() => progressDialog.ShowDialog()));
            
            await Task.Delay(100);
            
            progressDialog.UpdateMessage($"Splitting into {SplitPoints.Count + 1} parts...");

            var outputFiles = await Task.Run(() =>
            {
                // Create pattern using source filename
                var wavPattern = $"{baseFileName}_part_{{0}}.wav";
                
                var wavFiles = _audioEditor.SplitAudioFile(
                    inputFilePath,
                    folderDialog.FolderName,
                    wavPattern,
                    SplitPoints.ToList());

                // Convert to MP3 if compression is enabled
                if (CompressToMp3)
                {
                    progressDialog?.UpdateMessage($"Compressing {wavFiles.Count} files to MP3...");
                    var mp3Files = new System.Collections.Generic.List<string>();

                    for (int i = 0; i < wavFiles.Count; i++)
                    {
                        var wavFile = wavFiles[i];
                        var mp3File = Path.Combine(folderDialog.FolderName, $"{baseFileName}_part_{i + 1}.mp3");
                        
                        ConvertWavToMp3(wavFile, mp3File, Mp3Bitrate);
                        mp3Files.Add(mp3File);
                        
                        // Delete temp WAV file
                        File.Delete(wavFile);
                    }

                    return mp3Files;
                }

                return wavFiles;
            });

            StatusMessage = $"Successfully split into {outputFiles.Count} files!";
            MessageBox.Show(
                $"Audio file split successfully!\n\nCreated {outputFiles.Count} files in:\n{folderDialog.FolderName}",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Reset state after successful split
            ResetState();
        }
        catch (Exception ex)
        {
            StatusMessage = "Error during split";
            MessageBox.Show($"Error splitting file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
            progressDialog?.Close();
        }
    }

    private void ResetState()
    {
        // Stop and unload audio player
        if (IsPlaying || IsPaused)
        {
            Stop();
        }
        _audioPlayer.Stop();

        // Clear all state
        LoadedFilePath = null;
        LoadedFileName = string.Empty;
        TotalDuration = TimeSpan.Zero;
        CurrentPosition = TimeSpan.Zero;
        SliderPosition = 0;
        IsFileLoaded = false;
        SplitPoints.Clear();
        NewSplitPointText = "00:00.000";
        OutputPattern = "part_{0}.wav";
        CompressToMp3 = false;
        Mp3Bitrate = 128;
        StatusMessage = "Ready to load a new file";
    }

    private void ConvertWavToMp3(string inputPath, string outputPath, int bitrate)
    {
        using var reader = new AudioFileReader(inputPath);
        using var writer = new LameMP3FileWriter(outputPath, reader.WaveFormat, bitrate);
        
        // Use 4-second buffer for better performance
        var buffer = new byte[reader.WaveFormat.AverageBytesPerSecond * 4];
        int bytesRead;
        while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            writer.Write(buffer, 0, bytesRead);
        }
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return ts.ToString(@"hh\:mm\:ss\.fff");
        return ts.ToString(@"mm\:ss\.fff");
    }

    private static TimeSpan ParseTimeSpan(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return TimeSpan.Zero;

        input = input.Trim();

        // Try with hours first (hh:mm:ss.fff or hh:mm:ss)
        if (TimeSpan.TryParseExact(input, @"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture, out var timeSpan))
            return timeSpan;
        if (TimeSpan.TryParseExact(input, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out timeSpan))
            return timeSpan;

        // Try without hours (mm:ss.fff or mm:ss)
        if (TimeSpan.TryParseExact(input, @"mm\:ss\.fff", CultureInfo.InvariantCulture, out timeSpan))
            return timeSpan;
        if (TimeSpan.TryParseExact(input, @"mm\:ss", CultureInfo.InvariantCulture, out timeSpan))
            return timeSpan;

        // Try with 2-digit or 1-digit milliseconds
        if (TimeSpan.TryParseExact(input, @"mm\:ss\.ff", CultureInfo.InvariantCulture, out timeSpan))
            return timeSpan;
        if (TimeSpan.TryParseExact(input, @"mm\:ss\.f", CultureInfo.InvariantCulture, out timeSpan))
            return timeSpan;

        // Try just seconds as decimal (e.g., "45.5")
        if (double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            return TimeSpan.FromSeconds(seconds);

        // Try standard TimeSpan parsing
        if (TimeSpan.TryParse(input, CultureInfo.InvariantCulture, out timeSpan))
            return timeSpan;

        return TimeSpan.Zero;
    }

    public void Cleanup()
    {
        // Stop audio playback
        if (IsPlaying)
        {
            _audioPlayer.Stop();
            IsPlaying = false;
            _positionTimer.Stop();
        }

        // Reset state
        LoadedFilePath = null;
        LoadedFileName = string.Empty;
        IsFileLoaded = false;
        IsPaused = false;
        SplitPoints.Clear();
        CurrentPosition = TimeSpan.Zero;
        SliderPosition = 0;
        TotalDuration = TimeSpan.Zero;
    }
}
