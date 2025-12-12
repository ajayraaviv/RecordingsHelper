using System;
using System.Collections.ObjectModel;
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
    }

    [RelayCommand]
    private void SetEndTime()
    {
        NewSegmentEnd = CurrentPosition;
    }

    [RelayCommand]
    private void AddSegment()
    {
        if (NewSegmentEnd <= NewSegmentStart)
        {
            MessageBox.Show("End time must be greater than start time.", "Invalid Segment", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (NewSegmentEnd > TotalDuration)
        {
            MessageBox.Show("End time cannot exceed the total duration.", "Invalid Segment", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var segment = new RedactionSegment
        {
            StartTime = NewSegmentStart,
            EndTime = NewSegmentEnd
        };

        RedactionSegments.Add(segment);
        StatusMessage = $"Added segment: {segment.StartTimeFormatted} - {segment.EndTimeFormatted}";

        // Reset inputs
        NewSegmentStart = TimeSpan.Zero;
        NewSegmentEnd = TimeSpan.Zero;
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
