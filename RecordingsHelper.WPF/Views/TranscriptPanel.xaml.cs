using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using RecordingsHelper.Core.Models;

namespace RecordingsHelper.WPF.Views;

public partial class TranscriptPanel : UserControl
{
    public TranscriptPanel()
    {
        InitializeComponent();
        
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is TranscriptPanelViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is TranscriptPanelViewModel newViewModel)
        {
            newViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TranscriptPanelViewModel.SelectedIndex) && sender is TranscriptPanelViewModel viewModel)
        {
            var index = viewModel.SelectedIndex;
            if (TranscriptList.Items.Count > index && index >= 0)
            {
                TranscriptList.ScrollIntoView(TranscriptList.Items[index]);
            }
        }
    }
}

public partial class TranscriptPanelViewModel : ObservableObject
{
    private ObservableCollection<TranscriptionSegment> _allSegments = new();
    private bool _isUserScrolling = false;
    private string _loadedTranscriptPath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<TranscriptionSegment> _filteredSegments = new();

    [ObservableProperty]
    private string _transcriptFileName = "No transcript loaded";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<SpeakerMapping> _speakerMappings = new();

    [ObservableProperty]
    private bool _showSpeakerEditor = false;

    [ObservableProperty]
    private int _selectedIndex = -1;

    [ObservableProperty]
    private bool _hasTranscript = false;

    [ObservableProperty]
    private bool _isCollapsed = false;

    [ObservableProperty]
    private string _collapseIcon;

    [ObservableProperty]
    private int _filteredSegmentCount = 0;

    [ObservableProperty]
    private int _totalSegmentCount = 0;

    public string SegmentCountText => HasTranscript ? $"{FilteredSegmentCount} of {TotalSegmentCount} segments" : string.Empty;

    public event EventHandler<TimeSpan>? SegmentSelected;

    public TranscriptPanelViewModel(string collapseDirection)
    {
        CollapseIcon = collapseDirection == "Left" ? "ChevronLeft" : "ChevronRight";
    }

    partial void OnSearchTextChanged(string value)
    {
        _isUserScrolling = true;
        FilterSegments();
        _isUserScrolling = false;
    }

    partial void OnFilteredSegmentCountChanged(int value)
    {
        OnPropertyChanged(nameof(SegmentCountText));
    }

    partial void OnTotalSegmentCountChanged(int value)
    {
        OnPropertyChanged(nameof(SegmentCountText));
    }

    partial void OnHasTranscriptChanged(bool value)
    {
        OnPropertyChanged(nameof(SegmentCountText));
    }

    partial void OnSelectedIndexChanged(int oldValue, int newValue)
    {
        // Only trigger audio seek if user manually selected (not from auto-scroll)
        if (newValue >= 0 && newValue < FilteredSegments.Count && !_isUserScrolling)
        {
            var segment = FilteredSegments[newValue];
            SegmentSelected?.Invoke(this, segment.StartTime);
        }
    }

    [RelayCommand]
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
                
                using var doc = JsonDocument.Parse(json);
                var segments = new ObservableCollection<TranscriptionSegment>();

                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var segment = new TranscriptionSegment();

                    if (element.TryGetProperty("start", out var start) || element.TryGetProperty("Start", out start) || element.TryGetProperty("StartTime", out start))
                    {
                        segment.StartTime = ParseTimeSpan(start.GetString());
                    }

                    if (element.TryGetProperty("end", out var end) || element.TryGetProperty("End", out end) || element.TryGetProperty("EndTime", out end))
                    {
                        segment.EndTime = ParseTimeSpan(end.GetString());
                    }

                    if (element.TryGetProperty("speaker", out var speaker) || element.TryGetProperty("Speaker", out speaker))
                    {
                        segment.Speaker = speaker.GetString() ?? "Unknown";
                    }

                    if (element.TryGetProperty("originalSpeaker", out var originalSpeaker) || element.TryGetProperty("OriginalSpeaker", out originalSpeaker))
                    {
                        segment.OriginalSpeaker = originalSpeaker.GetString();
                    }

                    if (element.TryGetProperty("text", out var text) || element.TryGetProperty("Text", out text))
                    {
                        segment.Text = text.GetString() ?? string.Empty;
                    }

                    if (element.TryGetProperty("confidence", out var confidence) || element.TryGetProperty("Confidence", out confidence))
                    {
                        segment.Confidence = confidence.GetDouble();
                    }

                    segments.Add(segment);
                }

                if (segments.Any())
                {
                    _allSegments = segments;
                    FilteredSegments = new ObservableCollection<TranscriptionSegment>(segments);
                    HasTranscript = true;
                    _loadedTranscriptPath = openFileDialog.FileName;
                    TranscriptFileName = Path.GetFileName(openFileDialog.FileName);
                    SearchText = string.Empty;
                    TotalSegmentCount = segments.Count;
                    FilteredSegmentCount = segments.Count;
                    
                    // Extract unique speakers and create mappings
                    LoadSpeakerMappings();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading transcript: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void ClearTranscript()
    {
        _allSegments.Clear();
        FilteredSegments.Clear();
        HasTranscript = false;
        TranscriptFileName = "No transcript loaded";
        _loadedTranscriptPath = string.Empty;
        SelectedIndex = -1;
        SearchText = string.Empty;
        TotalSegmentCount = 0;
        FilteredSegmentCount = 0;
        SpeakerMappings.Clear();
        ShowSpeakerEditor = false;
    }

    [RelayCommand]
    private void ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;
    }

    private void FilterSegments()
    {
        var currentSelectedSegment = SelectedIndex >= 0 && SelectedIndex < FilteredSegments.Count 
            ? FilteredSegments[SelectedIndex] 
            : null;

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredSegments = new ObservableCollection<TranscriptionSegment>(_allSegments);
            FilteredSegmentCount = _allSegments.Count;
        }
        else
        {
            var filtered = _allSegments.Where(s => 
                s.Text.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                s.Speaker.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
            FilteredSegments = new ObservableCollection<TranscriptionSegment>(filtered);
            FilteredSegmentCount = filtered.Count;
        }

        // Try to maintain selection on the same segment if it's still in the filtered list
        if (currentSelectedSegment != null)
        {
            var newIndex = FilteredSegments.IndexOf(currentSelectedSegment);
            SelectedIndex = newIndex;
        }
        else
        {
            SelectedIndex = -1;
        }

        OnPropertyChanged(nameof(SegmentCountText));
    }

    public void UpdateScrollPosition(TimeSpan currentTime)
    {
        if (!HasTranscript || !FilteredSegments.Any())
            return;

        _isUserScrolling = true;
        var index = FindSegmentIndexAtTime(currentTime);
        if (index >= 0 && index < FilteredSegments.Count)
        {
            SelectedIndex = index;
        }
        _isUserScrolling = false;
    }

    private int FindSegmentIndexAtTime(TimeSpan time)
    {
        for (int i = 0; i < FilteredSegments.Count; i++)
        {
            var segment = FilteredSegments[i];
            if (time >= segment.StartTime && time <= segment.EndTime)
            {
                return i;
            }
        }

        for (int i = 0; i < FilteredSegments.Count - 1; i++)
        {
            if (time >= FilteredSegments[i].EndTime && time < FilteredSegments[i + 1].StartTime)
            {
                return i;
            }
        }

        if (time > FilteredSegments[FilteredSegments.Count - 1].EndTime)
        {
            return FilteredSegments.Count - 1;
        }

        return 0;
    }

    private TimeSpan ParseTimeSpan(string? timeString)
    {
        if (string.IsNullOrEmpty(timeString))
            return TimeSpan.Zero;

        if (TimeSpan.TryParse(timeString, out var result))
            return result;

        if (double.TryParse(timeString, out var seconds))
            return TimeSpan.FromSeconds(seconds);

        return TimeSpan.Zero;
    }

    public void Cleanup()
    {
        _allSegments.Clear();
        FilteredSegments.Clear();
        HasTranscript = false;
        SelectedIndex = -1;
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void ToggleSpeakerEditor()
    {
        ShowSpeakerEditor = !ShowSpeakerEditor;
    }

    private void LoadSpeakerMappings()
    {
        var uniqueSpeakers = _allSegments
            .Select(s => s.Speaker)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        SpeakerMappings.Clear();
        foreach (var speaker in uniqueSpeakers)
        {
            SpeakerMappings.Add(new SpeakerMapping
            {
                OriginalName = speaker,
                NewName = speaker
            });
        }
    }

    [RelayCommand]
    private async Task UpdateSpeakers()
    {
        if (string.IsNullOrEmpty(_loadedTranscriptPath) || !File.Exists(_loadedTranscriptPath))
        {
            MessageBox.Show("Cannot save: Transcript file path is not available.", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            // Validate for duplicate speaker names
            var newNames = SpeakerMappings
                .Select(m => m.NewName.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            var duplicates = newNames
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Any())
            {
                MessageBox.Show(
                    $"Duplicate speaker names detected:\n\n{string.Join("\n", duplicates)}\n\nEach speaker must have a unique name.",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Validate for empty names
            var emptyNames = SpeakerMappings
                .Where(m => string.IsNullOrWhiteSpace(m.NewName))
                .Select(m => m.OriginalName)
                .ToList();

            if (emptyNames.Any())
            {
                MessageBox.Show(
                    $"The following speakers cannot have empty names:\n\n{string.Join("\n", emptyNames)}",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Create mapping dictionary from changes
            var mappings = SpeakerMappings
                .Where(m => m.OriginalName != m.NewName.Trim())
                .ToDictionary(m => m.OriginalName, m => m.NewName.Trim());

            if (!mappings.Any())
            {
                MessageBox.Show("No speaker changes detected.", "Info", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Update all segments in memory - UI will auto-refresh due to INotifyPropertyChanged
            foreach (var segment in _allSegments)
            {
                if (mappings.TryGetValue(segment.Speaker, out var newName))
                {
                    // Set OriginalSpeaker on first rename (if not already set)
                    if (string.IsNullOrEmpty(segment.OriginalSpeaker))
                    {
                        segment.OriginalSpeaker = segment.Speaker;
                    }
                    
                    segment.Speaker = newName;
                }
            }

            // Save to file
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var segmentsToSave = _allSegments.Select(s => new
            {
                speaker = s.Speaker,
                originalSpeaker = s.OriginalSpeaker,
                start = s.StartTime.ToString(@"hh\:mm\:ss\.fff"),
                end = s.EndTime.ToString(@"hh\:mm\:ss\.fff"),
                text = s.Text,
                confidence = s.Confidence
            }).ToList();

            var json = JsonSerializer.Serialize(segmentsToSave, options);
            await File.WriteAllTextAsync(_loadedTranscriptPath, json);

            // Update mappings to reflect new state
            LoadSpeakerMappings();

            MessageBox.Show($"Speaker names updated and saved to {Path.GetFileName(_loadedTranscriptPath)}", 
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error updating speakers: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

public partial class SpeakerMapping : ObservableObject
{
    [ObservableProperty]
    private string _originalName = string.Empty;

    [ObservableProperty]
    private string _newName = string.Empty;
}
