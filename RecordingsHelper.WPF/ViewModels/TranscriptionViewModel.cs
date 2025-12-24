using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NAudio.Wave;
using RecordingsHelper.Core.Models;
using RecordingsHelper.Core.Services;
using RecordingsHelper.WPF.Services;
using RecordingsHelper.WPF.Views;

namespace RecordingsHelper.WPF.ViewModels;

public partial class TranscriptionViewModel : ObservableObject, IDisposable
{
    private readonly AudioPlayerService _audioPlayer;
    private readonly TranscriptionService _transcriptionService;
    private readonly DispatcherTimer _positionTimer;
    private const string SettingsFileName = "transcription-settings.json";
    private CancellationTokenSource? _cancellationTokenSource;

    private const string DefaultLlmPrompt = @"This is a Social Security Administration disability hearing conducted by the Office of Hearings Operations.
The hearing is presided over by an Administrative Law Judge and follows formal SSA adjudication procedures.
The Administrative Law Judge asks structured legal questions, provides instructions, and issues rulings on the record.
The claimant provides testimony regarding impairments, symptoms, work history, and functional limitations and may speak informally or with hesitation.
A vocational expert provides testimony regarding past relevant work, residual functional capacity, exertional levels, skill levels, SVP ratings, and DOT job classifications.
A representative or attorney may be present and speaks using formal legal advocacy language.
A medical expert may testify using clinical terminology related to diagnoses, impairments, and functional assessments.
A hearing reporter may announce procedural statements and manage the official hearing record.
An interpreter or translator may repeat or translate statements between languages as part of the hearing.
Legal, medical, and vocational terminology should be transcribed precisely using standard SSA terminology.
Questions and answers should remain distinct and clearly punctuated.
Statements should use proper grammar and punctuation without altering the original meaning or intent.
Pauses, partial sentences, clarifications, and corrections may occur and should be preserved as spoken.";


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
    [NotifyCanExecuteChangedFor(nameof(StartTranscriptionCommand))]
    private bool _isFileLoaded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartTranscriptionCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopTranscriptionCommand))]
    private bool _isTranscribing;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<TranscriptionSegment> _transcriptionSegments = new();

    [ObservableProperty]
    private TranscriptionSettings _settings = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartTranscriptionCommand))]
    private bool _hasSettings;

    [ObservableProperty]
    private double _transcriptionProgress;

    // Transcription options (per-transcription settings)
    [ObservableProperty]
    private bool _useLlmEnhancement = false;

    [ObservableProperty]
    private bool _enableDiarization = true;

    [ObservableProperty]
    private int _maxSpeakers = 6;

    [ObservableProperty]
    private string? _llmPrompt;

    [ObservableProperty]
    private bool _showLlmPrompt = false;

    [ObservableProperty]
    private bool _showSpeakerLabels = true;

    [ObservableProperty]
    private TranscriptionSegment? _activeSegment;

    [ObservableProperty]
    private bool _autoScrollEnabled = true;

    [ObservableProperty]
    private bool _uploadToBlobStorage = false;

    [ObservableProperty]
    private string? _blobUrl;

    [ObservableProperty]
    private string _profanityFilterMode = "Masked";

    public string[] ProfanityFilterModes { get; } = new[] { "None", "Masked", "Removed", "Tags" };

    public Action<TranscriptionSegment>? ScrollToActiveSegment { get; set; }

    public int[] AvailableSpeakers { get; } = Enumerable.Range(1, 20).ToArray();

    public TranscriptionViewModel()
    {
        _audioPlayer = new AudioPlayerService();
        _transcriptionService = new TranscriptionService();
        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _positionTimer.Tick += OnPositionTimerTick;

        // Initialize LLM prompt with default
        _llmPrompt = DefaultLlmPrompt;

        LoadSettings();
    }

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (_audioPlayer.IsPlaying)
        {
            CurrentPosition = _audioPlayer.Position;
            if (TotalDuration.TotalSeconds > 0)
            {
                SliderPosition = CurrentPosition.TotalSeconds;
            }

            // Update active segment based on current position
            var currentSegment = TranscriptionSegments.FirstOrDefault(s => 
                CurrentPosition >= s.StartTime && CurrentPosition <= s.EndTime);
            
            if (currentSegment != null && currentSegment != ActiveSegment)
            {
                ActiveSegment = currentSegment;
                
                // Auto-scroll to active segment if enabled
                if (AutoScrollEnabled && ScrollToActiveSegment != null)
                {
                    ScrollToActiveSegment(currentSegment);
                }
            }
        }
    }

    [RelayCommand]
    private void LoadFile()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Audio Files|*.wav;*.mp3;*.m4a;*.mp4;*.wma;*.aac|All Files|*.*",
            Title = "Select Audio File to Transcribe"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                LoadedFilePath = openFileDialog.FileName;
                LoadedFileName = Path.GetFileName(LoadedFilePath);

                using var reader = new AudioFileReader(LoadedFilePath);
                TotalDuration = reader.TotalTime;
                SliderPosition = 0;
                CurrentPosition = TimeSpan.Zero;
                IsFileLoaded = true;
                TranscriptionSegments.Clear();
                BlobUrl = null;  // Clear blob URL when new file is loaded
                StatusMessage = "File loaded successfully. Click 'Start Transcription' to begin.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void Clear()
    {
        Cleanup();
        StatusMessage = "Ready. Load an audio file to begin.";
    }

    [RelayCommand]
    private void PlayPause()
    {
        if (LoadedFilePath == null) return;

        if (IsPlaying)
        {
            _audioPlayer.Pause();
            IsPlaying = false;
            IsPaused = true;
            _positionTimer.Stop();
        }
        else
        {
            if (!IsPaused)
            {
                _audioPlayer.LoadFile(LoadedFilePath);
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

        var newPosition = TimeSpan.FromSeconds(value);
        if (Math.Abs((newPosition - CurrentPosition).TotalMilliseconds) > 200)
        {
            _audioPlayer.Position = newPosition;
            CurrentPosition = newPosition;
        }
    }

    [RelayCommand]
    private void Stop()
    {
        _audioPlayer.Stop();
        IsPlaying = false;
        IsPaused = false;
        CurrentPosition = TimeSpan.Zero;
        SliderPosition = 0;
        _positionTimer.Stop();
    }

    [RelayCommand]
    private void SliderChanged(double position)
    {
        if (Math.Abs(position - CurrentPosition.TotalSeconds) > 0.5)
        {
            _audioPlayer.Position = TimeSpan.FromSeconds(position);
            CurrentPosition = TimeSpan.FromSeconds(position);
        }
    }

    [RelayCommand]
    private void CopyBlobUrl()
    {
        if (!string.IsNullOrEmpty(BlobUrl))
        {
            Clipboard.SetText(BlobUrl);
            StatusMessage = "Blob URL copied to clipboard";
        }
    }

    [RelayCommand]
    private void SeekToSegment(TranscriptionSegment? segment)
    {
        if (segment == null) return;

        // Seek to the start of the segment
        _audioPlayer.Position = segment.StartTime;
        CurrentPosition = segment.StartTime;
        SliderPosition = segment.StartTime.TotalSeconds;
        
        // Set as active segment
        ActiveSegment = segment;
        
        // If not playing, start playing
        if (!_audioPlayer.IsPlaying && !string.IsNullOrEmpty(LoadedFilePath))
        {
            if (!IsPaused)
            {
                _audioPlayer.LoadFile(LoadedFilePath);
            }
            _audioPlayer.Play();
            IsPlaying = true;
            IsPaused = false;
            _positionTimer.Start();
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settingsDialog = new TranscriptionSettingsDialog
        {
            Owner = Application.Current.MainWindow,
            DataContext = Settings
        };

        if (settingsDialog.ShowDialog() == true)
        {
            SaveSettings();
            StatusMessage = "Settings saved successfully.";
        }
    }

    [RelayCommand]
    private void OpenLlmPrompt()
    {
        var dialog = new LlmPromptDialog(LlmPrompt)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            LlmPrompt = dialog.PromptText;
            StatusMessage = "LLM prompt updated.";
        }
    }

    [RelayCommand]
    private void ToggleLlmPrompt()
    {
        ShowLlmPrompt = !ShowLlmPrompt;
    }

    [RelayCommand(CanExecute = nameof(CanStartTranscription))]
    private async Task StartTranscriptionAsync()
    {
        if (string.IsNullOrWhiteSpace(Settings.SubscriptionKey) || string.IsNullOrWhiteSpace(Settings.Region))
        {
            MessageBox.Show("Please configure Azure Speech settings first.", "Settings Required", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            OpenSettings();
            return;
        }

        if (LoadedFilePath == null) return;

        IsTranscribing = true;
        TranscriptionProgress = 0;
        TranscriptionSegments.Clear();
        StatusMessage = "Initializing transcription...";
        _cancellationTokenSource = new CancellationTokenSource();

        var progressDialog = new ProgressDialog
        {
            Owner = Application.Current.MainWindow,
            Title = "Transcription in Progress",
            ShowCancelButton = true
        };
        
        progressDialog.SetCancellationTokenSource(_cancellationTokenSource);

        var dialogTask = Task.Run(() =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                progressDialog.ShowDialog();
            });
        });

        try
        {
            var progressReporter = new Progress<Core.Models.TranscriptionProgress>(p =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    progressDialog.UpdateMessage(p.Message);
                    TranscriptionProgress = p.ProgressPercentage;
                });
            });

            // Upload to blob storage if requested
            if (UploadToBlobStorage && !string.IsNullOrWhiteSpace(BlobUrl))
            {
                StatusMessage = "Using existing blob URL...";
            }
            else if (UploadToBlobStorage)
            {
                if (string.IsNullOrWhiteSpace(Settings.StorageAccountName) || 
                    string.IsNullOrWhiteSpace(Settings.StorageAccountKey) ||
                    string.IsNullOrWhiteSpace(Settings.StorageContainerName))
                {
                    MessageBox.Show("Please configure storage account settings first.", "Storage Settings Required",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    OpenSettings();
                    return;
                }

                StatusMessage = "Uploading to blob storage...";
                var uploadFolder = "fasttranscription";
                
                BlobUrl = await _transcriptionService.UploadToBlobStorageAsync(
                    LoadedFilePath,
                    Settings.StorageAccountName,
                    Settings.StorageAccountKey,
                    Settings.StorageContainerName,
                    uploadFolder,
                    _cancellationTokenSource.Token);
                
                StatusMessage = $"Uploaded to storage. Using blob URL for transcription.";
            }

            var options = new Core.Models.TranscriptionOptions
            {
                Mode = Core.Models.TranscriptionMode.Fast,
                UseLlmEnhancement = UseLlmEnhancement,
                EnableDiarization = EnableDiarization,
                MaxSpeakers = MaxSpeakers,
                LlmPrompt = UseLlmEnhancement ? LlmPrompt : null,
                ProfanityFilterMode = ProfanityFilterMode
            };

            List<TranscriptionSegment> segments;
            
            if (UploadToBlobStorage && !string.IsNullOrWhiteSpace(BlobUrl))
            {
                // Use blob URL for transcription
                segments = await _transcriptionService.TranscribeAudioFromBlobAsync(
                    BlobUrl,
                    LoadedFilePath,
                    Settings,
                    options,
                    progressReporter,
                    _cancellationTokenSource.Token);
            }
            else
            {
                // Traditional file upload
                segments = await _transcriptionService.TranscribeAudioAsync(
                    LoadedFilePath,
                    Settings,
                    options,
                    progressReporter,
                    _cancellationTokenSource.Token);
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var segment in segments)
                {
                    TranscriptionSegments.Add(segment);
                }
                
                // Determine if we should show speaker labels (more than one unique speaker)
                var uniqueSpeakers = segments.Select(s => s.Speaker).Distinct().Count();
                ShowSpeakerLabels = uniqueSpeakers > 1;
                
                StatusMessage = $"Transcription completed. {TranscriptionSegments.Count} segments found.";
            });
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Transcription canceled.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Transcription error: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsTranscribing = false;
            Application.Current.Dispatcher.Invoke(() =>
            {
                progressDialog.Close();
            });
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private bool CanStartTranscription() => IsFileLoaded && !IsTranscribing && HasSettings;

    [RelayCommand(CanExecute = nameof(CanStopTranscription))]
    private void StopTranscription()
    {
        _cancellationTokenSource?.Cancel();
        StatusMessage = "Stopping transcription...";
    }

    private bool CanStopTranscription() => IsTranscribing;

    [RelayCommand]
    private async Task ExportTranscriptionAsync()
    {
        if (TranscriptionSegments.Count == 0)
        {
            MessageBox.Show("No transcription to export.", "Export", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveFileDialog = new SaveFileDialog
        {
            Filter = "JSON Files|*.json|Text Files|*.txt|All Files|*.*",
            FileName = Path.GetFileNameWithoutExtension(LoadedFileName) + "_transcription.json"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                var segments = TranscriptionSegments.ToList();
                var extension = Path.GetExtension(saveFileDialog.FileName).ToLowerInvariant();

                if (extension == ".txt")
                {
                    await _transcriptionService.ExportToTextAsync(segments, saveFileDialog.FileName);
                }
                else
                {
                    await _transcriptionService.ExportToJsonAsync(segments, saveFileDialog.FileName);
                }

                StatusMessage = "Transcription exported successfully.";
                MessageBox.Show("Transcription exported successfully.", "Export", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting transcription: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void LoadSettings()
    {
        try
        {
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RecordingsHelper");

            if (!Directory.Exists(appDataFolder))
                Directory.CreateDirectory(appDataFolder);

            var settingsPath = Path.Combine(appDataFolder, SettingsFileName);

            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize<TranscriptionSettings>(json);
                if (settings != null)
                {
                    Settings = settings;
                    HasSettings = !string.IsNullOrWhiteSpace(settings.SubscriptionKey) && 
                                  !string.IsNullOrWhiteSpace(settings.Region);
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading settings: {ex.Message}";
        }
    }

    private void SaveSettings()
    {
        try
        {
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RecordingsHelper");

            if (!Directory.Exists(appDataFolder))
                Directory.CreateDirectory(appDataFolder);

            var settingsPath = Path.Combine(appDataFolder, SettingsFileName);
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, json);
            
            HasSettings = !string.IsNullOrWhiteSpace(Settings.SubscriptionKey) && 
                          !string.IsNullOrWhiteSpace(Settings.Region);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving settings: {ex.Message}", "Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
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

        // Cancel any ongoing transcription
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        // Reset state
        LoadedFilePath = null;
        LoadedFileName = string.Empty;
        IsFileLoaded = false;
        IsTranscribing = false;
        TranscriptionSegments.Clear();
        BlobUrl = null;
        TranscriptionProgress = 0;
        CurrentPosition = TimeSpan.Zero;
        SliderPosition = 0;
        StatusMessage = string.Empty;
        ActiveSegment = null;
    }

    public void Dispose()
    {
        _positionTimer?.Stop();
        _audioPlayer?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}
