using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NAudio.Wave;
using RecordingsHelper.WPF.Services;
using RecordingsHelper.WPF.Views;

namespace RecordingsHelper.WPF.ViewModels;

public partial class TranscriptComparisonViewModel : ObservableObject, IDisposable
{
    private readonly AudioPlayerService _audioPlayer;
    private readonly DispatcherTimer _positionTimer;

    public TranscriptPanelViewModel LeftPanel { get; }
    public TranscriptPanelViewModel RightPanel { get; }

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
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isPlaying;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isPaused;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isFileLoaded;



    public TranscriptComparisonViewModel()
    {
        _audioPlayer = new AudioPlayerService();
        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _positionTimer.Tick += OnPositionTimerTick;

        LeftPanel = new TranscriptPanelViewModel("Left");
        RightPanel = new TranscriptPanelViewModel("Right");

        LeftPanel.SegmentSelected += OnSegmentSelected;
        RightPanel.SegmentSelected += OnSegmentSelected;
    }

    private void OnSegmentSelected(object? sender, TimeSpan time)
    {
        SeekToTime(time);
    }

    private void SeekToTime(TimeSpan time)
    {
        if (!IsFileLoaded) return;
        
        _audioPlayer.Position = time;
        CurrentPosition = time;
        SliderPosition = time.TotalMilliseconds;
    }

    [RelayCommand]
    private void LoadAudioFile()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Audio Files (*.wav;*.mp3)|*.wav;*.mp3|All Files (*.*)|*.*",
            Title = "Select Audio File"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                _audioPlayer.LoadFile(openFileDialog.FileName);
                LoadedFilePath = openFileDialog.FileName;
                LoadedFileName = Path.GetFileName(openFileDialog.FileName);
                TotalDuration = _audioPlayer.Duration;
                IsFileLoaded = true;
                CurrentPosition = TimeSpan.Zero;
                SliderPosition = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading audio file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanPlayPause))]
    private void PlayPause()
    {
        if (IsPlaying)
        {
            _audioPlayer.Pause();
            IsPaused = true;
            IsPlaying = false;
            _positionTimer.Stop();
        }
        else
        {
            _audioPlayer.Play();
            IsPlaying = true;
            IsPaused = false;
            _positionTimer.Start();
        }
        
        PlayPauseCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }

    private bool CanPlayPause() => IsFileLoaded;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _audioPlayer.Stop();
        IsPlaying = false;
        IsPaused = false;
        CurrentPosition = TimeSpan.Zero;
        SliderPosition = 0;
        _positionTimer.Stop();
        
        PlayPauseCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }

    private bool CanStop() => IsFileLoaded && (IsPlaying || IsPaused);



    partial void OnSliderPositionChanged(double value)
    {
        if (!IsFileLoaded) return;

        var newPosition = TimeSpan.FromMilliseconds(value);
        if (Math.Abs((newPosition - CurrentPosition).TotalMilliseconds) > 200)
        {
            _audioPlayer.Position = newPosition;
            CurrentPosition = newPosition;
            UpdateScrollPositions();
        }
    }

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (!IsPlaying) return;

        CurrentPosition = _audioPlayer.Position;
        SliderPosition = CurrentPosition.TotalMilliseconds;
        UpdateScrollPositions();
    }

    private void UpdateScrollPositions()
    {
        LeftPanel.UpdateScrollPosition(CurrentPosition);
        RightPanel.UpdateScrollPosition(CurrentPosition);
    }



    public void Cleanup()
    {
        _positionTimer.Stop();
        _audioPlayer.Stop();
        
        // Reset all state
        IsPlaying = false;
        IsPaused = false;
        IsFileLoaded = false;
        LoadedFilePath = null;
        LoadedFileName = string.Empty;
        CurrentPosition = TimeSpan.Zero;
        SliderPosition = 0;
        TotalDuration = TimeSpan.Zero;
        
        // Clear transcripts
        LeftPanel.ClearTranscriptCommand.Execute(null);
        RightPanel.ClearTranscriptCommand.Execute(null);
        
        // Notify command states
        PlayPauseCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _positionTimer?.Stop();
        _audioPlayer?.Dispose();
    }
}
