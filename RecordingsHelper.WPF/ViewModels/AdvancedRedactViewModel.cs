using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Lame;
using RecordingsHelper.Core.Models;
using RecordingsHelper.Core.Services;
using RecordingsHelper.WPF.Views;

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
            await Task.Delay(300, token); // Increased debounce for better performance
            if (!token.IsCancellationRequested)
            {
                // Do filtering off the UI thread
                var searchText = value;
                var items = string.IsNullOrWhiteSpace(searchText)
                    ? TranscriptItems.ToList()
                    : TranscriptItems.Where(t =>
                        (!string.IsNullOrEmpty(t.Text) && t.Text.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                        || t.SpeakerId.ToString().Contains(searchText)
                    ).ToList();

                // Debug: Log matching items
                System.Diagnostics.Debug.WriteLine($"Search: '{searchText}' found {items.Count} items");
                foreach (var item in items)
                {
                    System.Diagnostics.Debug.WriteLine($"  ID={item.Id}, Text='{item.Text.Substring(0, Math.Min(50, item.Text.Length))}...', Time={item.TimeRange}");
                }

                if (!token.IsCancellationRequested)
                {
                    // Update UI on UI thread with pre-filtered results
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        FilteredTranscriptItems.Clear();
                        foreach (var item in items)
                        {
                            FilteredTranscriptItems.Add(item);
                        }

                        // Update status
                        if (!string.IsNullOrWhiteSpace(searchText))
                        {
                            StatusMessage = $"Found {items.Count} matching segment{(items.Count != 1 ? "s" : "")}.";
                        }
                        else
                        {
                            StatusMessage = string.Empty;
                        }
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }, token);
    }        private readonly AudioEditor _audioEditor = new();


        [ObservableProperty]
        private string? _loadedAudioPath;

        [ObservableProperty]
        private string? _loadedInsightsPath;

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _compressToMp3;

        [ObservableProperty]
        private int _mp3Bitrate = 128;

        public int[] AvailableBitrates { get; } = { 64, 96, 128, 160, 192, 256, 320 };


        public ObservableCollection<TranscriptItemViewModel> TranscriptItems { get; } = new();

        public void Cleanup()
        {
            // Cancel any ongoing search
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = null;

            // Reset state
            SearchText = string.Empty;
            FilteredTranscriptItems.Clear();
            TranscriptItems.Clear();
        }

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

                    // Create items for each instance and collect them
                    var allItems = new List<TranscriptItemViewModel>();
                    foreach (var transcript in transcripts)
                    {
                        // Create a separate item for each instance
                        foreach (var instance in transcript.Instances)
                        {
                            allItems.Add(new TranscriptItemViewModel(transcript, instance, 0)); // Temporary ID
                        }
                    }

                    // Remove duplicates based on AdjustedStart and AdjustedEnd
                    var uniqueItems = allItems
                        .GroupBy(item => new { item.Instance.AdjustedStart, item.Instance.AdjustedEnd })
                        .Select(g => g.First())
                        .ToList();

                    // Sort by start time and assign sequential IDs
                    var sortedItems = uniqueItems.OrderBy(item => item.StartTime).ToList();
                    for (int i = 0; i < sortedItems.Count; i++)
                    {
                        sortedItems[i].Id = i + 1;
                    }

                    // Add to collections
                    TranscriptItems.Clear();
                    foreach (var item in sortedItems)
                    {
                        TranscriptItems.Add(item);
                    }

                    // Initialize filtered items with all items
                    FilteredTranscriptItems.Clear();
                    foreach (var item in TranscriptItems)
                    {
                        FilteredTranscriptItems.Add(item);
                    }
                    
                    StatusMessage = $"Loaded {TranscriptItems.Count} unique transcript segments from {transcripts.Count} transcript entries";
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
                Filter = CompressToMp3 ? "MP3 Files|*.mp3" : "WAV Files|*.wav",
                Title = "Save Redacted Audio",
                FileName = CompressToMp3
                    ? Path.GetFileNameWithoutExtension(LoadedAudioPath) + "_redacted.mp3"
                    : Path.GetFileNameWithoutExtension(LoadedAudioPath) + "_redacted.wav"
            };

            if (saveDialog.ShowDialog() != true)
                return;

            IsProcessing = true;
            StatusMessage = $"Processing {selectedItems.Count} segments...";

            ProgressDialog? progressDialog = null;

            try
            {
                // Show progress dialog on UI thread
                progressDialog = new ProgressDialog { Title = "Processing Redactions", Owner = Application.Current.MainWindow };
                
                // Show dialog asynchronously so it doesn't block
                var dialogTask = Task.Run(() => Application.Current.Dispatcher.Invoke(() => progressDialog.ShowDialog()));
                
                // Give the dialog time to render
                await Task.Delay(100);
                
                progressDialog.UpdateMessage($"Processing {selectedItems.Count} segments...");

                await Task.Run(() =>
                {
                    // Group segments by action type
                    var removeSegments = new List<AudioSegment>();
                    var muteSegments = new List<AudioSegment>();

                    foreach (var item in selectedItems)
                    {
                        var segment = new AudioSegment(item.StartTime, item.EndTime);

                        if (item.SelectedAction == RedactionAction.Remove)
                            removeSegments.Add(segment);
                        else
                            muteSegments.Add(segment);
                    }

                    var outputPath = saveDialog.FileName;
                    var finalWavPath = CompressToMp3
                        ? Path.Combine(Path.GetTempPath(), $"temp_final_{Guid.NewGuid()}.wav")
                        : outputPath;

                    var tempFile = LoadedAudioPath;

                    // Process MUTE segments first (doesn't change timeline)
                    if (muteSegments.Count > 0)
                    {
                        var outputFile = removeSegments.Count > 0 ? Path.GetTempFileName() : finalWavPath;
                        _audioEditor.MuteSegments(tempFile, outputFile, muteSegments);

                        if (tempFile != LoadedAudioPath && File.Exists(tempFile))
                            File.Delete(tempFile);

                        tempFile = outputFile;
                    }

                    // Process REMOVE segments after (changes timeline)
                    if (removeSegments.Count > 0)
                    {
                        _audioEditor.RemoveSegments(tempFile, finalWavPath, removeSegments);

                        if (tempFile != LoadedAudioPath && tempFile != finalWavPath && File.Exists(tempFile))
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
                progressDialog?.Close();
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

    private void ConvertWavToMp3(string inputPath, string outputPath, int bitrate)
    {
        using var reader = new AudioFileReader(inputPath);
        using var writer = new LameMP3FileWriter(outputPath, reader.WaveFormat, bitrate);
        reader.CopyTo(writer);
    }
}    public enum RedactionAction
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
        public TranscriptInstance Instance { get; }

        public int Id { get; set; }
        public string Text => Transcript.Text;
        public double Confidence => Instance.Confidence;
        public int SpeakerId => Transcript.SpeakerId;
        public string TimeRange => $"{FormatTimeSpan(Instance.StartTime)} - {FormatTimeSpan(Instance.EndTime)}";
        public TimeSpan StartTime => Instance.StartTime;
        public TimeSpan EndTime => Instance.EndTime;

        public TranscriptItemViewModel(TranscriptInsight transcript, TranscriptInstance instance, int id)
        {
            Transcript = transcript;
            Instance = instance;
            Id = id;
        }

        private static string FormatTimeSpan(TimeSpan time)
        {
            if (time.TotalHours >= 1)
                return time.ToString(@"h\:mm\:ss\.fff");
            return time.ToString(@"m\:ss\.fff");
        }
    }
}