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
using RecordingsHelper.Core.Models;
using RecordingsHelper.Core.Services;
using RecordingsHelper.WPF.Models;
using RecordingsHelper.WPF.Services;

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
    private bool _muteSegments = false; // false = remove, true = mute

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
    private void Play()
    {
        if (!IsFileLoaded) return;

        _audioPlayer.Play();
        IsPlaying = true;
        IsPaused = false;
        _positionTimer.Start();
    }

    [RelayCommand]
    private void Pause()
    {
        if (!IsFileLoaded) return;

        _audioPlayer.Pause();
        IsPlaying = false;
        IsPaused = true;
        _positionTimer.Stop();
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

        var segment = new RedactionSegment
        {
            StartTime = start,
            EndTime = end
        };

        RedactionSegments.Add(segment);
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
            Filter = "WAV Files|*.wav",
            Title = "Save Redacted Audio File",
            FileName = $"redacted_{Path.GetFileNameWithoutExtension(LoadedFilePath)}.wav"
        };

        if (saveFileDialog.ShowDialog() != true)
            return;

        // Stop playback before processing
        if (IsPlaying || IsPaused)
        {
            Stop();
        }

        IsProcessing = true;
        StatusMessage = MuteSegments ? "Muting segments..." : "Removing segments...";

        try
        {
            await Task.Run(() =>
            {
                var segments = RedactionSegments
                    .Select(s => AudioSegment.FromDuration(s.StartTime, s.Duration))
                    .ToList();

                if (MuteSegments)
                {
                    _audioEditor.MuteSegments(LoadedFilePath!, saveFileDialog.FileName, segments);
                }
                else
                {
                    _audioEditor.RemoveSegments(LoadedFilePath!, saveFileDialog.FileName, segments);
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
        MuteSegments = false;
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

    public void Dispose()
    {
        _positionTimer.Stop();
        _audioPlayer.Dispose();
    }
}
