using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using RecordingsHelper.Core.Models;
using RecordingsHelper.Core.Services;

namespace RecordingsHelper.WPF.ViewModels
{

    public partial class AdvancedRedactViewModel : ObservableObject
    {
        public AdvancedRedactViewModel() { }

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<TranscriptItemViewModel> FilteredTranscriptItems { get; } = new();
    private CancellationTokenSource? _searchCts;

    partial void OnSearchTextChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        
        Task.Run(async () =>
        {
            await Task.Delay(150, token); // Debounce
            if (!token.IsCancellationRequested)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => UpdateFilteredItems());
            }
        }, token);
    }

    private void UpdateFilteredItems()
    {
        var items = string.IsNullOrWhiteSpace(SearchText)
            ? TranscriptItems.ToList()
            : TranscriptItems.Where(t =>
                (!string.IsNullOrEmpty(t.Text) && t.Text.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                || t.SpeakerId.ToString().Contains(SearchText)
            ).ToList();

        FilteredTranscriptItems.Clear();
        foreach (var item in items)
        {
            FilteredTranscriptItems.Add(item);
        }
    }        private readonly AudioEditor _audioEditor = new();


        [ObservableProperty]
        private string? _loadedAudioPath;

        [ObservableProperty]
        private string? _loadedInsightsPath;

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private string _statusMessage = string.Empty;


        public ObservableCollection<TranscriptItemViewModel> TranscriptItems { get; } = new();

        [RelayCommand]
        private void LoadAudioFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Audio Files|*.wav;*.mp3;*.ogg;*.flac;*.m4a;*.aac|All Files|*.*",
                Title = "Select Audio File"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadedAudioPath = dialog.FileName;
                StatusMessage = $"Loaded: {Path.GetFileName(LoadedAudioPath)}";
            }
        }

        [RelayCommand]
        private void LoadInsightsFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON Files|*.json|All Files|*.*",
                Title = "Select Insights JSON File"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    LoadedInsightsPath = dialog.FileName;
                    var transcripts = InsightsParser.LoadFromJson(dialog.FileName);

                    TranscriptItems.Clear();
                    foreach (var transcript in transcripts)
                    {
                        TranscriptItems.Add(new TranscriptItemViewModel(transcript));
                    }

                    UpdateFilteredItems();
                    StatusMessage = $"Loaded {transcripts.Count} transcript segments";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Error loading insights: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var item in TranscriptItems)
            {
                item.IsSelected = true;
            }
        }

        [RelayCommand]
        private void DeselectAll()
        {
            foreach (var item in TranscriptItems)
            {
                item.IsSelected = false;
            }
        }

        [RelayCommand]
        private async Task ProcessRedactions()
        {
            if (string.IsNullOrEmpty(LoadedAudioPath))
            {
                StatusMessage = "Please load an audio file first";
                return;
            }

            var selectedItems = TranscriptItems.Where(t => t.IsSelected).ToList();
            if (selectedItems.Count == 0)
            {
                StatusMessage = "Please select at least one transcript segment";
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "WAV Files|*.wav|MP3 Files|*.mp3|OGG Files|*.ogg",
                Title = "Save Redacted Audio",
                FileName = Path.GetFileNameWithoutExtension(LoadedAudioPath) + "_redacted"
            };

            if (saveDialog.ShowDialog() != true)
                return;

            IsProcessing = true;
            StatusMessage = $"Processing {selectedItems.Count} segments...";

            try
            {
                await Task.Run(() =>
                {
                    // Group segments by action type
                    var removeSegments = new List<AudioSegment>();
                    var muteSegments = new List<AudioSegment>();

                    foreach (var item in selectedItems)
                    {
                        foreach (var instance in item.Transcript.Instances)
                        {
                            var segment = new AudioSegment(instance.StartTime, instance.EndTime);

                            if (item.SelectedAction == RedactionAction.Remove)
                                removeSegments.Add(segment);
                            else
                                muteSegments.Add(segment);
                        }
                    }

                    var tempFile = LoadedAudioPath;

                    // Process MUTE segments first (doesn't change timeline)
                    if (muteSegments.Count > 0)
                    {
                        var outputFile = removeSegments.Count > 0 ? Path.GetTempFileName() : saveDialog.FileName;
                        _audioEditor.MuteSegments(tempFile, outputFile, muteSegments);

                        if (tempFile != LoadedAudioPath && File.Exists(tempFile))
                            File.Delete(tempFile);

                        tempFile = outputFile;
                    }

                    // Process REMOVE segments after (changes timeline)
                    if (removeSegments.Count > 0)
                    {
                        _audioEditor.RemoveSegments(tempFile, saveDialog.FileName, removeSegments);

                        if (tempFile != LoadedAudioPath && tempFile != saveDialog.FileName && File.Exists(tempFile))
                            File.Delete(tempFile);
                    }
                });

                StatusMessage = "Redaction completed successfully!";
                ResetState();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

    [RelayCommand]
    private void UnloadAll()
    {
        LoadedAudioPath = null;
        LoadedInsightsPath = null;
        TranscriptItems.Clear();
        FilteredTranscriptItems.Clear();
        StatusMessage = string.Empty;
    }

    private void ResetState()
    {
        LoadedAudioPath = null;
        LoadedInsightsPath = null;
        TranscriptItems.Clear();
        FilteredTranscriptItems.Clear();
        StatusMessage = string.Empty;
    }
    }

    public enum RedactionAction
    {
        Mute,
        Remove
    }

    public partial class TranscriptItemViewModel : ObservableObject
    {

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private RedactionAction _selectedAction = RedactionAction.Mute;

        // Helper property for ToggleButton binding
        public bool IsRemoveAction
        {
            get => SelectedAction == RedactionAction.Remove;
            set => SelectedAction = value ? RedactionAction.Remove : RedactionAction.Mute;
        }

        public TranscriptInsight Transcript { get; }

        public int Id => Transcript.Id;
        public string Text => Transcript.Text;
        public double Confidence => Transcript.Confidence;
        public int SpeakerId => Transcript.SpeakerId;
        public string TimeRange
        {
            get
            {
                if (Transcript.Instances.Count == 0)
                    return "No timing data";

                var first = Transcript.Instances[0];
                var last = Transcript.Instances[^1];
                return $"{FormatTimeSpan(first.StartTime)} - {FormatTimeSpan(last.EndTime)}";
            }
        }

        public TranscriptItemViewModel(TranscriptInsight transcript)
        {
            Transcript = transcript;
        }

        private static string FormatTimeSpan(TimeSpan time)
        {
            if (time.TotalHours >= 1)
                return time.ToString(@"h\:mm\:ss\.fff");
            return time.ToString(@"m\:ss\.fff");
        }
    }
}