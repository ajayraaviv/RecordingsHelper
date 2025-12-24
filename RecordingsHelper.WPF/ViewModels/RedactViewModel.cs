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
using RecordingsHelper.Core.Models;
using RecordingsHelper.Core.Services;
using RecordingsHelper.WPF.Models;
using RecordingsHelper.WPF.Services;
using RecordingsHelper.WPF.Views;

namespace RecordingsHelper.WPF.ViewModels;

public partial class RedactViewModel : ObservableObject, IDisposable
{
    private readonly AudioPlayerService _audioPlayer;
    private readonly AudioEditor _audioEditor;
    private readonly DispatcherTimer _positionTimer;

    [ObservableProperty]
    private string? _loadedFilePath;

    [ObservableProperty]
    private string _loadedFileName = string.Empty;

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
    private bool _isFileLoaded;

    [ObservableProperty]
    private ObservableCollection<RedactionSegment> _redactionSegments = new();

    [ObservableProperty]
    private TimeSpan _newSegmentStart;

    [ObservableProperty]
    private TimeSpan _newSegmentEnd;
    
    // String properties for free text entry
    [ObservableProperty]
    private string _startTimeText = "00:00.000";
    
    [ObservableProperty]
    private string _endTimeText = "00:00.000";

    partial void OnStartTimeTextChanged(string value)
    {
        var parsed = ParseTimeSpan(value);
        NewSegmentStart = parsed;
        // Format the text after parsing (on LostFocus this will show formatted version)
        if (!string.IsNullOrWhiteSpace(value) && value != FormatTimeSpanHelper(parsed))
        {
            StartTimeText = FormatTimeSpanHelper(parsed);
        }
    }
    
    partial void OnEndTimeTextChanged(string value)
    {
        var parsed = ParseTimeSpan(value);
        NewSegmentEnd = parsed;
        // Format the text after parsing (on LostFocus this will show formatted version)
        if (!string.IsNullOrWhiteSpace(value) && value != FormatTimeSpanHelper(parsed))
        {
            EndTimeText = FormatTimeSpanHelper(parsed);
        }
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

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _muteSegments = true;

    [ObservableProperty]
    private bool _compressToMp3;

    [ObservableProperty]
    private int _mp3Bitrate = 128;

    public int[] AvailableBitrates { get; } = { 64, 96, 128, 160, 192, 256, 320 }; // false = remove, true = mute
    
    [ObservableProperty]
    private bool _applyToAll = true;
    
    partial void OnApplyToAllChanged(bool value)
    {
        if (value)
        {
            // When switching to "Apply to All", set all segments to use global action
            foreach (var segment in RedactionSegments)
            {
                segment.UseGlobalAction = true;
            }
        }
        else
        {
            // When switching to "Per Segment", uncheck all segments so they can be customized
            foreach (var segment in RedactionSegments)
            {
                segment.UseGlobalAction = false;
            }
        }
    }
    
    partial void OnMuteSegmentsChanged(bool value)
    {
        OnPropertyChanged(nameof(RemoveCount));
        OnPropertyChanged(nameof(MuteCount));
    }
    
    public int RemoveCount
    {
        get
        {
            if (ApplyToAll)
                return MuteSegments ? 0 : RedactionSegments.Count;
            
            // In Per Segment mode, count segments where MuteSegment is false
            return RedactionSegments.Count(s => !s.MuteSegment);
        }
    }
    
    public int MuteCount
    {
        get
        {
            if (ApplyToAll)
                return MuteSegments ? RedactionSegments.Count : 0;
            
            // In Per Segment mode, count segments where MuteSegment is true
            return RedactionSegments.Count(s => s.MuteSegment);
        }
    }

    public RedactViewModel()
    {
        _audioPlayer = new AudioPlayerService();
        _audioEditor = new AudioEditor();

        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _positionTimer.Tick += (s, e) =>
        {
            if (_audioPlayer.IsPlaying)
            {
                CurrentPosition = _audioPlayer.Position;
            }
        };

        _audioPlayer.PlaybackStopped += (s, e) =>
        {
            IsPlaying = false;
            IsPaused = false;
        };
    }

    [RelayCommand]
    private void LoadFile()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Audio Files|*.wav;*.mp3;*.ogg;*.mp4;*.m4a;*.aac;*.wma;*.aiff|All Files|*.*",
            Title = "Select Audio File to Redact"
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
                RedactionSegments.Clear();
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
        
        _audioPlayer.Stop();
        LoadedFilePath = null;
        LoadedFileName = string.Empty;
        TotalDuration = TimeSpan.Zero;
        CurrentPosition = TimeSpan.Zero;
        SliderPosition = 0;
        IsFileLoaded = false;
        RedactionSegments.Clear();
        NewSegmentStart = TimeSpan.Zero;
        NewSegmentEnd = TimeSpan.Zero;
        StatusMessage = "File unloaded";
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
        if (!IsFileLoaded) return;

        _audioPlayer.Stop();
        _audioPlayer.LoadFile(LoadedFilePath!);
        IsPlaying = false;
        IsPaused = false;
        CurrentPosition = TimeSpan.Zero;
        _positionTimer.Stop();
    }

    [RelayCommand]
    private void SetStartTime()
    {
        NewSegmentStart = CurrentPosition;
        StartTimeText = FormatTimeSpanHelper(CurrentPosition);
    }

    [RelayCommand]
    private void SetEndTime()
    {
        NewSegmentEnd = CurrentPosition;
        EndTimeText = FormatTimeSpanHelper(CurrentPosition);
    }
    
    private static string FormatTimeSpanHelper(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return ts.ToString(@"hh\:mm\:ss\.fff");
        return ts.ToString(@"mm\:ss\.fff");
    }

    [RelayCommand]
    private void AddSegment()
    {
        // Parse current text values to ensure we have latest input
        var start = ParseTimeSpan(StartTimeText);
        var end = ParseTimeSpan(EndTimeText);
        
        if (end <= start)
        {
            MessageBox.Show("End time must be greater than start time.", "Invalid Segment", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (end > TotalDuration)
        {
            MessageBox.Show("End time cannot exceed the total duration.", "Invalid Segment", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Check for overlaps with existing segments
        foreach (var existing in RedactionSegments)
        {
            // Check if new segment overlaps with existing segment
            if ((start >= existing.StartTime && start < existing.EndTime) ||  // New start is within existing
                (end > existing.StartTime && end <= existing.EndTime) ||      // New end is within existing
                (start <= existing.StartTime && end >= existing.EndTime))     // New segment contains existing
            {
                var result = MessageBox.Show(
                    $"This segment overlaps with an existing segment ({existing.StartTimeFormatted} - {existing.EndTimeFormatted}).\n\nDo you want to add it anyway?",
                    "Overlap Detected",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result != MessageBoxResult.Yes)
                    return;
                
                break; // Only show warning once
            }
        }

        var segment = new RedactionSegment
        {
            StartTime = start,
            EndTime = end
        };
        
        // Subscribe to property changes on the new segment
        segment.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RedactionSegment.MuteSegment))
            {
                OnPropertyChanged(nameof(RemoveCount));
                OnPropertyChanged(nameof(MuteCount));
            }
        };

        RedactionSegments.Add(segment);
        OnPropertyChanged(nameof(RemoveCount));
        OnPropertyChanged(nameof(MuteCount));
        StatusMessage = $"Added segment: {segment.StartTimeFormatted} - {segment.EndTimeFormatted}";

        // Reset inputs
        NewSegmentStart = TimeSpan.Zero;
        NewSegmentEnd = TimeSpan.Zero;
        StartTimeText = "00:00.000";
        EndTimeText = "00:00.000";
    }

    [RelayCommand]
    private void RemoveSegment(RedactionSegment segment)
    {
        RedactionSegments.Remove(segment);
        OnPropertyChanged(nameof(RemoveCount));
        OnPropertyChanged(nameof(MuteCount));
        StatusMessage = $"Removed segment: {segment.StartTimeFormatted} - {segment.EndTimeFormatted}";
    }

    [RelayCommand]
    private void ClearSegments()
    {
        RedactionSegments.Clear();
        StatusMessage = "Cleared all segments";
    }

    [RelayCommand]
    private async Task ProcessFile()
    {
        if (!IsFileLoaded)
        {
            MessageBox.Show("Please load an audio file first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (RedactionSegments.Count == 0)
        {
            MessageBox.Show("Please add at least one redaction segment.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var saveFileDialog = new SaveFileDialog
        {
            Filter = CompressToMp3 ? "MP3 Files|*.mp3" : "WAV Files|*.wav",
            Title = "Save Redacted Audio File",
            FileName = CompressToMp3 
                ? $"{Path.GetFileNameWithoutExtension(LoadedFilePath)}_redacted.mp3"
                : $"{Path.GetFileNameWithoutExtension(LoadedFilePath)}_redacted.wav"
        };

        if (saveFileDialog.ShowDialog() != true)
            return;

        // Store the file path before unloading
        var inputFilePath = LoadedFilePath!;

        // Stop playback and release file handles before processing
        if (IsPlaying || IsPaused)
        {
            Stop();
        }
        _audioPlayer.Stop(); // Ensure file handle is released

        IsProcessing = true;
        StatusMessage = "Processing file...";

        ProgressDialog? progressDialog = null;

        try
        {
            // Show progress dialog on UI thread
            progressDialog = new ProgressDialog { Title = "Processing Redaction", Owner = Application.Current.MainWindow };
            
            // Show dialog asynchronously so it doesn't block
            var dialogTask = Task.Run(() => Application.Current.Dispatcher.Invoke(() => progressDialog.ShowDialog()));
            
            // Give the dialog time to render
            await Task.Delay(100);
            
            progressDialog.UpdateMessage("Preparing segments...");

            // Separate segments into mute and remove lists based on per-segment or global setting
            var segmentsToMute = new List<AudioSegment>();
            var segmentsToRemove = new List<AudioSegment>();
            
            foreach (var segment in RedactionSegments.OrderBy(s => s.StartTime))
            {
                bool shouldMute = segment.UseGlobalAction ? MuteSegments : segment.MuteSegment;
                var audioSegment = AudioSegment.FromDuration(segment.StartTime, segment.Duration);
                
                if (shouldMute)
                    segmentsToMute.Add(audioSegment);
                else
                    segmentsToRemove.Add(audioSegment);
            }

            await Task.Run(() =>
            {
                var outputPath = saveFileDialog.FileName;
                var finalWavPath = CompressToMp3 
                    ? Path.Combine(Path.GetTempPath(), $"temp_final_{Guid.NewGuid()}.wav")
                    : outputPath;

                // First, mute segments if any
                string tempFile = inputFilePath;
                if (segmentsToMute.Count > 0)
                {
                    progressDialog?.UpdateMessage($"Muting {segmentsToMute.Count} segment(s)...");
                    tempFile = Path.Combine(Path.GetTempPath(), $"temp_muted_{Guid.NewGuid()}.wav");
                    _audioEditor.MuteSegments(inputFilePath, tempFile, segmentsToMute);
                }
                
                // Then, remove segments if any
                if (segmentsToRemove.Count > 0)
                {
                    progressDialog?.UpdateMessage($"Removing {segmentsToRemove.Count} segment(s)...");
                    _audioEditor.RemoveSegments(tempFile, finalWavPath, segmentsToRemove);
                    
                    // Clean up temp file if we created one
                    if (tempFile != inputFilePath && File.Exists(tempFile))
                        File.Delete(tempFile);
                }
                else if (segmentsToMute.Count > 0)
                {
                    // Only muting, no removal - copy temp file to final destination
                    File.Copy(tempFile, finalWavPath, true);
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }

                // Convert to MP3 if compression is enabled
                if (CompressToMp3)
                {
                    progressDialog?.UpdateMessage("Compressing to MP3...");
                    ConvertWavToMp3(finalWavPath, outputPath, Mp3Bitrate);
                    
                    // Clean up temporary WAV file
                    if (File.Exists(finalWavPath))
                        File.Delete(finalWavPath);
                }
            });

            StatusMessage = $"Successfully processed {RedactionSegments.Count} segments!";
            MessageBox.Show($"Audio file processed successfully!\n\nSaved to: {saveFileDialog.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            
            // Reset state after successful processing
            ResetState();
        }
        catch (Exception ex)
        {
            StatusMessage = "Error during processing";
            MessageBox.Show($"Error processing file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        RedactionSegments.Clear();
        NewSegmentStart = TimeSpan.Zero;
        NewSegmentEnd = TimeSpan.Zero;
        StartTimeText = "00:00.000";
        EndTimeText = "00:00.000";
        MuteSegments = true;
        ApplyToAll = true;
        CompressToMp3 = false;
        Mp3Bitrate = 128;
        StatusMessage = "Ready to load a new file";
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

    private void ConvertWavToMp3(string inputPath, string outputPath, int bitrate)
    {
        using var reader = new AudioFileReader(inputPath);
        using var writer = new LameMP3FileWriter(outputPath, reader.WaveFormat, bitrate);
        byte[] buffer = new byte[reader.WaveFormat.AverageBytesPerSecond * 4]; // 4 second buffer for better performance
        int bytesRead;
        while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            writer.Write(buffer, 0, bytesRead);
        }
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
        RedactionSegments.Clear();
        CurrentPosition = TimeSpan.Zero;
        SliderPosition = 0;
        NewSegmentStart = TimeSpan.Zero;
        NewSegmentEnd = TimeSpan.Zero;
        StartTimeText = "00:00.000";
        EndTimeText = "00:00.000";
    }

    public void Dispose()
    {
        _positionTimer.Stop();
        _audioPlayer.Dispose();
    }
}
