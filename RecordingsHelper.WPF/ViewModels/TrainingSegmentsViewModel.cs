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
using RecordingsHelper.WPF.Services;
using System.Text;
using System.Collections.Generic;

namespace RecordingsHelper.WPF.ViewModels;

public partial class TrainingSegmentsViewModel : ObservableObject, IDisposable
{
    private readonly AudioPlayerService _audioPlayer;
    private readonly DispatcherTimer _positionTimer;

    [ObservableProperty]
    private string? _loadedAudioFilePath;

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
    [NotifyCanExecuteChangedFor(nameof(LoadTranscriptCommand))]
    [NotifyCanExecuteChangedFor(nameof(ProcessSegmentsCommand))]
    private bool _isFileLoaded;

    [ObservableProperty]
    private ObservableCollection<TrainingSegmentItem> _segments = new();

    [ObservableProperty]
    private ObservableCollection<TrainingSegmentItem> _filteredSegments = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _showHiddenSegments = true;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private string _statusMessage = "Load an audio file and transcript to begin.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ProcessSegmentsCommand))]
    private bool _hasTranscript;

    [ObservableProperty]
    private TrainingSegmentItem? _activeSegment;

    public TrainingSegmentsViewModel()
    {
        _audioPlayer = new AudioPlayerService();
        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _positionTimer.Tick += OnPositionTimerTick;
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnShowHiddenSegmentsChanged(bool value)
    {
        ApplyFilters();
    }

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (!IsPlaying) return;

        CurrentPosition = _audioPlayer.Position;
        SliderPosition = CurrentPosition.TotalMilliseconds;

        // Update active segment based on current position
        // Note: We search all Segments (not FilteredSegments) to ensure playback
        // tracking works correctly even when segments are hidden or filtered
        var currentSegment = Segments.FirstOrDefault(s =>
            CurrentPosition >= s.StartTime && CurrentPosition <= s.EndTime);

        if (currentSegment != null && currentSegment != ActiveSegment)
        {
            ActiveSegment = currentSegment;
        }
    }

    [RelayCommand]
    private void LoadAudioFile()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Audio Files (*.wav;*.mp3;*.m4a;*.mp4)|*.wav;*.mp3;*.m4a;*.mp4|All Files (*.*)|*.*",
            Title = "Select Audio File"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                _audioPlayer.LoadFile(openFileDialog.FileName);
                LoadedAudioFilePath = openFileDialog.FileName;
                LoadedFileName = Path.GetFileName(openFileDialog.FileName);
                TotalDuration = _audioPlayer.Duration;
                IsFileLoaded = true;
                CurrentPosition = TimeSpan.Zero;
                SliderPosition = 0;
                StatusMessage = "Audio file loaded. Load a transcript to continue.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading audio file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoadTranscript))]
    private void LoadTranscript()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Select Transcript"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(openFileDialog.FileName);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var segments = new ObservableCollection<TrainingSegmentItem>();

                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var segment = new TrainingSegmentItem();

                    if (element.TryGetProperty("start", out var start) || element.TryGetProperty("Start", out start) || element.TryGetProperty("StartTime", out start))
                    {
                        segment.StartTime = ParseTimeSpan(start.GetString());
                    }

                    if (element.TryGetProperty("end", out var end) || element.TryGetProperty("End", out end) || element.TryGetProperty("EndTime", out end))
                    {
                        segment.EndTime = ParseTimeSpan(end.GetString());
                    }

                    if (element.TryGetProperty("text", out var text) || element.TryGetProperty("Text", out text))
                    {
                        segment.Text = text.GetString() ?? string.Empty;
                    }

                    if (element.TryGetProperty("speaker", out var speaker) || element.TryGetProperty("Speaker", out speaker))
                    {
                        segment.Speaker = speaker.GetString() ?? "Unknown";
                    }

                    segments.Add(segment);
                }

                Segments = segments;
                HasTranscript = true;
                UpdateStatusMessage();
                
                // Subscribe to property changes on segments to update command state
                foreach (var segment in Segments)
                {
                    segment.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(TrainingSegmentItem.IsSelected))
                        {
                            UpdateSelectedCount();
                            ProcessSegmentsCommand.NotifyCanExecuteChanged();
                        }
                        else if (e.PropertyName == nameof(TrainingSegmentItem.IsHidden))
                        {
                            // Prevent hiding selected segments
                            if (segment.IsHidden && segment.IsSelected)
                            {
                                segment.IsHidden = false;
                            }
                        }
                    };
                }
                
                // Apply initial filter
                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading transcript: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private bool CanLoadTranscript() => IsFileLoaded;

    private TimeSpan ParseTimeSpan(string? timeString)
    {
        if (string.IsNullOrWhiteSpace(timeString))
            return TimeSpan.Zero;

        if (TimeSpan.TryParse(timeString, out var result))
            return result;

        if (double.TryParse(timeString, out var seconds))
            return TimeSpan.FromSeconds(seconds);

        return TimeSpan.Zero;
    }

    [RelayCommand(CanExecute = nameof(CanPlayPause))]
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
            if (!IsPaused && LoadedAudioFilePath != null)
            {
                _audioPlayer.LoadFile(LoadedAudioFilePath);
            }
            _audioPlayer.Play();
            IsPlaying = true;
            IsPaused = false;
            _positionTimer.Start();
        }
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
    }

    private bool CanStop() => IsFileLoaded && (IsPlaying || IsPaused);

    [RelayCommand]
    private void SeekToSegment(TrainingSegmentItem? segment)
    {
        if (segment == null || !IsFileLoaded) return;

        _audioPlayer.Position = segment.StartTime;
        CurrentPosition = segment.StartTime;
        SliderPosition = segment.StartTime.TotalMilliseconds;
        ActiveSegment = segment;

        if (!IsPlaying && LoadedAudioFilePath != null)
        {
            if (!IsPaused)
            {
                _audioPlayer.LoadFile(LoadedAudioFilePath);
            }
            _audioPlayer.Play();
            IsPlaying = true;
            IsPaused = false;
            _positionTimer.Start();
        }
    }

    partial void OnSliderPositionChanged(double value)
    {
        if (!IsFileLoaded) return;

        var newPosition = TimeSpan.FromMilliseconds(value);
        if (Math.Abs((newPosition - CurrentPosition).TotalMilliseconds) > 200)
        {
            _audioPlayer.Position = newPosition;
            CurrentPosition = newPosition;
        }
    }

    [RelayCommand]
    private void ToggleSelectAll()
    {
        var allSelected = Segments.All(s => s.IsSelected);
        foreach (var segment in Segments)
        {
            segment.IsSelected = !allSelected;
        }
        UpdateSelectedCount();
        ProcessSegmentsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void MergeSelected()
    {
        var selectedSegments = Segments.Where(s => s.IsSelected).OrderBy(s => s.StartTime).ToList();
        
        if (selectedSegments.Count < 2)
        {
            MessageBox.Show("Please select at least 2 contiguous segments to merge.", "Merge Segments", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Check if segments are contiguous
        for (int i = 0; i < selectedSegments.Count - 1; i++)
        {
            var currentIndex = Segments.IndexOf(selectedSegments[i]);
            var nextIndex = Segments.IndexOf(selectedSegments[i + 1]);
            
            if (nextIndex != currentIndex + 1)
            {
                MessageBox.Show("Selected segments must be contiguous (next to each other).", "Merge Segments", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        // Create merged segment
        var mergedSegment = new TrainingSegmentItem
        {
            StartTime = selectedSegments.First().StartTime,
            EndTime = selectedSegments.Last().EndTime,
            Text = string.Join(" ", selectedSegments.Select(s => s.Text)),
            Speaker = selectedSegments.First().Speaker,
            IsSelected = true
        };

        // Remove selected segments and insert merged segment
        var firstIndex = Segments.IndexOf(selectedSegments.First());
        foreach (var segment in selectedSegments)
        {
            Segments.Remove(segment);
        }
        Segments.Insert(firstIndex, mergedSegment);
        
        // Subscribe to property changes on the new merged segment
        mergedSegment.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TrainingSegmentItem.IsSelected))
            {
                ProcessSegmentsCommand.NotifyCanExecuteChanged();
            }
        };

        MessageBox.Show($"Merged {selectedSegments.Count} segments into 1.\n\nNote: To restore original segments, reload the transcript.", 
            "Segments Merged", MessageBoxButton.OK, MessageBoxImage.Information);
        
        UpdateSelectedCount();
        ProcessSegmentsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanProcessSegments))]
    private async Task ProcessSegments()
    {
        var selectedSegments = Segments.Where(s => s.IsSelected).OrderBy(s => s.StartTime).ToList();
        
        if (selectedSegments.Count == 0)
        {
            MessageBox.Show("Please select at least one segment to process.", "Process Segments", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrEmpty(LoadedAudioFilePath))
            return;

        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Folder|*.folder",
            FileName = "Select Folder",
            Title = "Select Output Folder"
        };

        // Ask for output directory
        var folderDialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select output folder for training segments",
            ShowNewFolderButton = true
        };

        if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        var outputFolder = folderDialog.SelectedPath;

        try
        {
            StatusMessage = "Processing segments...";
            
            var baseFileName = Path.GetFileNameWithoutExtension(LoadedAudioFilePath);
            var extension = Path.GetExtension(LoadedAudioFilePath);
            var transcriptLines = new List<string>();

            for (int i = 0; i < selectedSegments.Count; i++)
            {
                var segment = selectedSegments[i];
                var partNumber = i + 1;
                var segmentFileName = $"{baseFileName}_segment_{partNumber}{extension}";
                var segmentFilePath = Path.Combine(outputFolder, segmentFileName);

                // Extract audio segment with millisecond precision (1/1000th second, exceeding 1/10th second requirement)
                await ExtractAudioSegment(LoadedAudioFilePath, segmentFilePath, segment.StartTime, segment.EndTime);
                
                // Add transcript line (filename\ttranscript)
                transcriptLines.Add($"{segmentFileName}\t{segment.Text}");
            }

            // Write transcript file
            var transcriptFilePath = Path.Combine(outputFolder, $"{baseFileName}_segments_transcript.txt");
            await File.WriteAllLinesAsync(transcriptFilePath, transcriptLines, Encoding.UTF8);

            StatusMessage = $"Successfully processed {selectedSegments.Count} segments.";
            MessageBox.Show($"Processed {selectedSegments.Count} segments.\n\nOutput folder: {outputFolder}", 
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            MessageBox.Show($"Error processing segments: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanProcessSegments()
    {
        if (!IsFileLoaded || !HasTranscript || Segments == null)
            return false;

        var hasSelected = Segments.Any(s => s.IsSelected);
        System.Diagnostics.Debug.WriteLine($"CanProcessSegments: IsFileLoaded={IsFileLoaded}, HasTranscript={HasTranscript}, SegmentCount={Segments.Count}, SelectedCount={Segments.Count(s => s.IsSelected)}, Result={hasSelected}");
        return hasSelected;
    }

    private async Task ExtractAudioSegment(string inputPath, string outputPath, TimeSpan start, TimeSpan end)
    {
        await Task.Run(() =>
        {
            using var reader = new AudioFileReader(inputPath);
            
            // Skip to start position
            reader.CurrentTime = start;
            
            // Calculate length to read
            var duration = end - start;
            var samplesToRead = (int)(duration.TotalSeconds * reader.WaveFormat.SampleRate * reader.WaveFormat.Channels);
            
            // Read segment
            var buffer = new float[samplesToRead];
            var samplesRead = reader.Read(buffer, 0, samplesToRead);
            
            // Write to output file
            using var writer = new WaveFileWriter(outputPath, reader.WaveFormat);
            writer.WriteSamples(buffer, 0, samplesRead);
        });
    }

    private void ApplyFilters()
    {
        if (Segments == null || Segments.Count == 0)
        {
            FilteredSegments.Clear();
            return;
        }

        var filtered = Segments.AsEnumerable();

        // Apply hidden filter
        if (!ShowHiddenSegments)
        {
            filtered = filtered.Where(s => !s.IsHidden);
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLower();
            filtered = filtered.Where(s => 
                s.Text.ToLower().Contains(searchLower) || 
                s.Speaker.ToLower().Contains(searchLower));
        }

        // Update collection efficiently to reduce flickering
        var filteredList = filtered.ToList();
        
        // Only update if the collection actually changed
        if (FilteredSegments.Count != filteredList.Count || 
            !FilteredSegments.SequenceEqual(filteredList))
        {
            FilteredSegments = new ObservableCollection<TrainingSegmentItem>(filteredList);
        }
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = Segments?.Count(s => s.IsSelected) ?? 0;
        UpdateStatusMessage();
    }

    private void UpdateStatusMessage()
    {
        if (!HasTranscript)
        {
            StatusMessage = "Load an audio file and transcript to begin.";
            return;
        }

        var totalSegments = Segments?.Count ?? 0;
        if (SelectedCount > 0)
        {
            StatusMessage = $"Loaded {totalSegments} segments. {SelectedCount} selected for extraction.";
        }
        else
        {
            StatusMessage = $"Loaded {totalSegments} segments. Select segments to extract.";
        }
    }

    [RelayCommand]
    private void ToggleHideSegment(TrainingSegmentItem segment)
    {
        if (segment == null) return;

        // Don't allow hiding selected segments (UI button should be hidden, but adding check for safety)
        if (segment.IsSelected)
            return;

        segment.IsHidden = !segment.IsHidden;
        
        // Apply filters without full rebuild to reduce flickering
        if (!ShowHiddenSegments)
        {
            ApplyFilters();
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
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

        // Reset all state
        LoadedAudioFilePath = null;
        LoadedFileName = string.Empty;
        IsFileLoaded = false;
        IsPaused = false;
        TotalDuration = TimeSpan.Zero;
        CurrentPosition = TimeSpan.Zero;
        SliderPosition = 0;
        
        Segments.Clear();
        FilteredSegments.Clear();
        
        SearchText = string.Empty;
        ShowHiddenSegments = true;
        SelectedCount = 0;
        HasTranscript = false;
        ActiveSegment = null;
        StatusMessage = "Load an audio file and transcript to begin.";
    }

    public void Dispose()
    {
        _positionTimer?.Stop();
        _audioPlayer?.Dispose();
    }
}

public partial class TrainingSegmentItem : ObservableObject
{
    [ObservableProperty]
    private TimeSpan _startTime;

    [ObservableProperty]
    private TimeSpan _endTime;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private string _speaker = "Unknown";

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isHidden;
}
