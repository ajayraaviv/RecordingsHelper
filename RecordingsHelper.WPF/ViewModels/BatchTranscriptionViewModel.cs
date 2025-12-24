using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using RecordingsHelper.Core.Models;
using RecordingsHelper.Core.Services;
using RecordingsHelper.WPF.Views;

namespace RecordingsHelper.WPF.ViewModels;

public partial class BatchTranscriptionViewModel : ObservableObject
{
    private readonly TranscriptionService _transcriptionService;
    private const string SettingsFileName = "transcription-settings.json";

    [ObservableProperty]
    private ObservableCollection<BatchTranscriptionItem> _items = new();

    [ObservableProperty]
    private TranscriptionSettings _settings = new();

    [ObservableProperty]
    private bool _enableDiarization = true;

    [ObservableProperty]
    private int _maxSpeakers = 6;

    [ObservableProperty]
    private string _profanityFilterMode = "Masked";

    public string[] ProfanityFilterModes { get; } = new[] { "None", "Masked", "Removed", "Tags" };

    [ObservableProperty]
    private ObservableCollection<SpeechModel> _availableModels = new();

    [ObservableProperty]
    private SpeechModel? _selectedModel;

    [ObservableProperty]
    private bool _isLoadingModels;

    [ObservableProperty]
    private bool _isSubmitting;

    public BatchTranscriptionViewModel()
    {
        _transcriptionService = new TranscriptionService();
        LoadSettings();
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
        }
    }

    [RelayCommand]
    private async Task LoadModels()
    {
        if (string.IsNullOrWhiteSpace(Settings.SubscriptionKey) || string.IsNullOrWhiteSpace(Settings.Region))
        {
            MessageBox.Show("Please configure Azure Speech settings using the gear button.", "Settings Required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsLoadingModels = true;
            var models = await _transcriptionService.GetAvailableModelsAsync(
                Settings.Region!,
                Settings.SubscriptionKey!,
                CancellationToken.None);

            // Update collection on UI thread to prevent threading issues
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Clear and re-add to prevent duplicates
                AvailableModels.Clear();
                foreach (var model in models)
                {
                    AvailableModels.Add(model);
                }
            });

            if (AvailableModels.Count == 0)
            {
                MessageBox.Show("No models found for this region.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading models: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoadingModels = false;
        }
    }

    [RelayCommand]
    private void AddFiles()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Audio Files (*.wav;*.mp3;*.mp4)|*.wav;*.mp3;*.mp4",
            Multiselect = true,
            Title = "Select Audio Files for Batch Transcription"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            foreach (var filePath in openFileDialog.FileNames)
            {
                // Check if file already exists in list
                if (!Items.Any(i => i.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                {
                    Items.Add(new BatchTranscriptionItem
                    {
                        FilePath = filePath,
                        Status = BatchTranscriptionStatus.NotStarted,
                        StatusMessage = "Ready to submit"
                    });
                }
            }
        }
    }

    [RelayCommand]
    private void RemoveFile(BatchTranscriptionItem item)
    {
        Items.Remove(item);
    }

    [RelayCommand]
    private void ClearCompleted()
    {
        var completed = Items.Where(i => i.Status == BatchTranscriptionStatus.Succeeded).ToList();
        foreach (var item in completed)
        {
            Items.Remove(item);
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        if (MessageBox.Show("Remove all files from the list?", "Clear All", 
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            Items.Clear();
        }
    }

    [RelayCommand]
    private void CopyBatchId(string? batchId)
    {
        if (!string.IsNullOrWhiteSpace(batchId))
        {
            try
            {
                Clipboard.SetText(batchId);
                MessageBox.Show("Batch ID copied to clipboard!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private async Task SubmitAll()
    {
        if (string.IsNullOrWhiteSpace(Settings.SubscriptionKey) || string.IsNullOrWhiteSpace(Settings.Region))
        {
            MessageBox.Show("Please configure Azure Speech settings using the gear button.", "Settings Required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Settings.StorageAccountName) || string.IsNullOrWhiteSpace(Settings.StorageAccountKey))
        {
            MessageBox.Show("Please configure Azure Storage Account settings using the gear button.", "Storage Required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveSettings();

        var notStartedItems = Items.Where(i => i.Status == BatchTranscriptionStatus.NotStarted).ToList();
        
        if (!notStartedItems.Any())
        {
            MessageBox.Show("No files ready to submit.", "Info",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Set submitting state immediately for instant UI feedback
        IsSubmitting = true;

        // Force UI update before async work begins
        await Task.Delay(1);

        try
        {
            // Upload all files to blob storage in parallel and collect their SAS URLs
            var uploadFolder = $"batch-{DateTime.UtcNow:yyyyMMddHHmmss}";

            // Create upload tasks for all items
            var uploadTasks = notStartedItems.Select(async item =>
            {
                item.Status = BatchTranscriptionStatus.Uploading;
                item.StatusMessage = "Uploading to storage...";

                var blobUrl = await _transcriptionService.UploadToBlobStorageAsync(
                    item.FilePath,
                    Settings.StorageAccountName,
                    Settings.StorageAccountKey,
                    Settings.StorageContainerName,
                    uploadFolder,
                    CancellationToken.None);

                item.BlobUrl = blobUrl;
                item.StatusMessage = "Uploaded to storage";
                
                return new { Item = item, BlobUrl = blobUrl };
            }).ToList();

            // Execute all uploads in parallel and collect results
            var uploadResults = await Task.WhenAll(uploadTasks);
            var blobUrls = uploadResults.Select(r => r.BlobUrl).ToList();

            // Create a single batch transcription for all files using individual blob URLs with SAS tokens
            var options = new TranscriptionOptions
            {
                Mode = TranscriptionMode.Batch,
                EnableDiarization = EnableDiarization,
                MaxSpeakers = MaxSpeakers,
                ProfanityFilterMode = ProfanityFilterMode,
                Model = SelectedModel?.SelfLink,
                ModelSupportsWordLevelTimestamps = SelectedModel?.SupportsWordLevelTimestamps ?? true
            };

            var batchTranscriptionId = await _transcriptionService.CreateBatchTranscriptionWithUrlsAsync(
                blobUrls, // Individual blob URLs with SAS tokens
                Settings,
                options,
                CancellationToken.None);

            // Update all items with the batch transcription ID
            foreach (var item in notStartedItems)
            {
                item.TranscriptionId = batchTranscriptionId;
                item.Status = BatchTranscriptionStatus.Submitted;
                item.StatusMessage = "Submitted";
                item.SubmittedAt = DateTime.Now;
            }

            MessageBox.Show($"Successfully submitted batch with {notStartedItems.Count} file(s) for transcription.\nBatch ID: {batchTranscriptionId}", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            foreach (var item in notStartedItems.Where(i => i.Status == BatchTranscriptionStatus.Uploading))
            {
                item.Status = BatchTranscriptionStatus.Failed;
                item.StatusMessage = "Failed to submit";
                item.ErrorMessage = ex.Message;
            }

            MessageBox.Show($"Error submitting batch: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    [RelayCommand]
    private async Task CheckStatusAll()
    {
        var submittedItems = Items.Where(i => !string.IsNullOrWhiteSpace(i.TranscriptionId) && 
            i.Status != BatchTranscriptionStatus.NotStarted &&
            i.Status != BatchTranscriptionStatus.Succeeded &&
            i.Status != BatchTranscriptionStatus.Failed).ToList();
            
        if (!submittedItems.Any())
        {
            MessageBox.Show("No submitted files to check. Please submit files first.", "Info",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(Settings.SubscriptionKey) || string.IsNullOrWhiteSpace(Settings.Region))
        {
            MessageBox.Show("Please configure Azure Speech settings using the gear button.", "Settings Required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Group items by their batch transcription ID
            var batchGroups = submittedItems.GroupBy(i => i.TranscriptionId).ToList();
            var totalSuccessCount = 0;
            var totalFailedCount = 0;
            var totalRunningCount = 0;

            foreach (var batchGroup in batchGroups)
            {
                var batchId = batchGroup.Key!;
                
                var status = await _transcriptionService.GetBatchTranscriptionStatusAsync(
                    Settings.Region!,
                    Settings.SubscriptionKey!,
                    batchId,
                    CancellationToken.None);

                // If succeeded, get the detailed report to update individual file statuses
                if (status.Status == BatchTranscriptionStatus.Succeeded)
                {
                    var report = await _transcriptionService.GetBatchTranscriptionReportAsync(
                        Settings.Region!,
                        Settings.SubscriptionKey!,
                        batchId,
                        CancellationToken.None);

                    if (report != null && report.RootElement.TryGetProperty("details", out var details))
                    {
                        // Update each item based on the report
                        foreach (var detail in details.EnumerateArray())
                        {
                            var source = detail.GetProperty("source").GetString();
                            var fileStatus = detail.GetProperty("status").GetString();

                            // Find matching item by blob URL
                            var item = Items.FirstOrDefault(i => 
                                i.TranscriptionId == batchId && 
                                i.BlobUrl != null && 
                                source != null && 
                                source.Contains(Path.GetFileName(new Uri(i.BlobUrl).LocalPath), StringComparison.OrdinalIgnoreCase));

                            if (item != null)
                            {
                                if (fileStatus == "Succeeded")
                                {
                                    item.Status = BatchTranscriptionStatus.Succeeded;
                                    item.StatusMessage = "Succeeded";
                                    item.CompletedAt = DateTime.Now;
                                    totalSuccessCount++;
                                }
                                else if (fileStatus == "Failed")
                                {
                                    item.Status = BatchTranscriptionStatus.Failed;
                                    item.StatusMessage = "Failed";
                                    
                                    // Get detailed error message if available
                                    var errorMessage = "Transcription failed for this file";
                                    if (detail.TryGetProperty("errorMessage", out var errorMsgProp))
                                    {
                                        errorMessage = errorMsgProp.GetString() ?? errorMessage;
                                    }
                                    else if (detail.TryGetProperty("errorKind", out var errorKindProp))
                                    {
                                        errorMessage = $"Error: {errorKindProp.GetString()}";
                                    }
                                    
                                    item.ErrorMessage = errorMessage;
                                    totalFailedCount++;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Fallback: update all items with the same status
                        foreach (var item in batchGroup)
                        {
                            item.Status = status.Status;
                            item.StatusMessage = status.StatusMessage;
                            item.CompletedAt = DateTime.Now;
                            totalSuccessCount++;
                        }
                    }
                }
                else if (status.Status == BatchTranscriptionStatus.Failed)
                {
                    // Update all items in this batch
                    foreach (var item in batchGroup)
                    {
                        item.Status = status.Status;
                        item.StatusMessage = status.StatusMessage;
                        item.ErrorMessage = status.ErrorMessage;
                        totalFailedCount++;
                    }
                }
                else
                {
                    // Update all items with running/submitted status
                    foreach (var item in batchGroup)
                    {
                        item.Status = status.Status;
                        item.StatusMessage = status.StatusMessage;
                        totalRunningCount++;
                    }
                }
            }

            // Show summary
            var message = new StringBuilder();
            message.AppendLine($"Checked {batchGroups.Count} batch(es):");
            if (totalSuccessCount > 0) message.AppendLine($"✓ Successful: {totalSuccessCount}");
            if (totalFailedCount > 0) message.AppendLine($"✗ Failed: {totalFailedCount}");
            if (totalRunningCount > 0) message.AppendLine($"⧗ Still running: {totalRunningCount}");

            var icon = totalFailedCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information;
            MessageBox.Show(message.ToString(), "Status Check Complete", MessageBoxButton.OK, icon);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error checking status:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task CheckStatus(BatchTranscriptionItem item)
    {
        if (string.IsNullOrWhiteSpace(item.TranscriptionId))
        {
            MessageBox.Show("No transcription ID found for this file.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (string.IsNullOrWhiteSpace(Settings.SubscriptionKey) || string.IsNullOrWhiteSpace(Settings.Region))
        {
            MessageBox.Show("Please configure Azure Speech settings using the gear button.", "Settings Required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var previousStatus = item.Status;
            item.StatusMessage = "Checking status...";

            var status = await _transcriptionService.GetBatchTranscriptionStatusAsync(
                Settings.Region!,
                Settings.SubscriptionKey!,
                item.TranscriptionId,
                CancellationToken.None);

            item.Status = status.Status;
            item.StatusMessage = status.StatusMessage;

            if (status.Status == BatchTranscriptionStatus.Succeeded && previousStatus != BatchTranscriptionStatus.Succeeded)
            {
                item.CompletedAt = DateTime.Now;
                MessageBox.Show($"Transcription completed for {item.FileName}", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (status.Status == BatchTranscriptionStatus.Failed)
            {
                item.ErrorMessage = status.ErrorMessage;
                MessageBox.Show($"Transcription failed for {item.FileName}: {status.ErrorMessage}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error checking status: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ViewTranscript(BatchTranscriptionItem item)
    {
        if (item.Status != BatchTranscriptionStatus.Succeeded)
        {
            MessageBox.Show("Transcription is not yet completed.", "Not Ready",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(Settings.SubscriptionKey) || string.IsNullOrWhiteSpace(Settings.Region))
        {
            MessageBox.Show("Please configure Azure Speech settings using the gear button.", "Settings Required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Download transcript if not already downloaded
            if (item.Segments == null || !item.Segments.Any())
            {
                item.StatusMessage = "Downloading transcript...";
                
                item.Segments = await _transcriptionService.DownloadBatchFileResultsAsync(
                    Settings.Region!,
                    Settings.SubscriptionKey!,
                    item.TranscriptionId,
                    item.BlobUrl,
                    CancellationToken.None);

                item.StatusMessage = "Completed";
            }

            // Show transcript in a dialog
            var transcriptText = GenerateTranscriptText(item.Segments);
            var viewer = new TranscriptViewerDialog(item.FileName, transcriptText);
            viewer.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error viewing transcript: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task SaveTranscript(BatchTranscriptionItem item)
    {
        if (item.Status != BatchTranscriptionStatus.Succeeded)
        {
            MessageBox.Show("Transcription is not yet completed.", "Not Ready",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(Settings.SubscriptionKey) || string.IsNullOrWhiteSpace(Settings.Region))
        {
            MessageBox.Show("Please configure Azure Speech settings using the gear button.", "Settings Required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Download transcript if not already downloaded
            if (item.Segments == null || !item.Segments.Any())
            {
                item.StatusMessage = "Downloading transcript...";
                
                item.Segments = await _transcriptionService.DownloadBatchFileResultsAsync(
                    Settings.Region!,
                    Settings.SubscriptionKey!,
                    item.TranscriptionId,
                    item.BlobUrl,
                    CancellationToken.None);

                item.StatusMessage = "Completed";
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|Text Files (*.txt)|*.txt",
                FileName = $"{Path.GetFileNameWithoutExtension(item.FilePath)}_transcript",
                Title = "Save Transcript"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var extension = Path.GetExtension(saveFileDialog.FileName).ToLowerInvariant();
                
                if (extension == ".json")
                {
                    // Merge adjacent segments from the same speaker before exporting
                    var mergedSegments = MergeAdjacentSegments(item.Segments);
                    await _transcriptionService.ExportToJsonAsync(mergedSegments, saveFileDialog.FileName);
                }
                else
                {
                    var transcriptText = GenerateTranscriptText(item.Segments);
                    await File.WriteAllTextAsync(saveFileDialog.FileName, transcriptText);
                }

                MessageBox.Show("Transcript saved successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving transcript: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private List<TranscriptionSegment> MergeAdjacentSegments(List<TranscriptionSegment> segments)
    {
        if (segments == null || !segments.Any())
            return new List<TranscriptionSegment>();

        var mergedSegments = new List<TranscriptionSegment>();
        var sortedSegments = segments.OrderBy(s => s.StartTime).ToList();

        TranscriptionSegment? currentMerged = null;

        foreach (var segment in sortedSegments)
        {
            if (currentMerged == null)
            {
                // Start a new merged segment
                currentMerged = new TranscriptionSegment
                {
                    StartTime = segment.StartTime,
                    EndTime = segment.EndTime,
                    Speaker = segment.Speaker,
                    Text = segment.Text,
                    Confidence = segment.Confidence
                };
            }
            else if (currentMerged.Speaker == segment.Speaker)
            {
                // Same speaker - merge the segments
                currentMerged.EndTime = segment.EndTime;
                currentMerged.Text += " " + segment.Text;
                // Average the confidence
                currentMerged.Confidence = (currentMerged.Confidence + segment.Confidence) / 2.0;
            }
            else
            {
                // Different speaker - save current and start a new one
                mergedSegments.Add(currentMerged);
                currentMerged = new TranscriptionSegment
                {
                    StartTime = segment.StartTime,
                    EndTime = segment.EndTime,
                    Speaker = segment.Speaker,
                    Text = segment.Text,
                    Confidence = segment.Confidence
                };
            }
        }

        // Don't forget to add the last merged segment
        if (currentMerged != null)
        {
            mergedSegments.Add(currentMerged);
        }

        return mergedSegments;
    }

    private string GenerateTranscriptText(System.Collections.Generic.List<TranscriptionSegment> segments)
    {
        var sb = new StringBuilder();
        string? currentSpeaker = null;

        foreach (var segment in segments.OrderBy(s => s.StartTime))
        {
            if (segment.Speaker != currentSpeaker)
            {
                if (currentSpeaker != null)
                    sb.AppendLine();
                
                sb.AppendLine($"{segment.Speaker}:");
                currentSpeaker = segment.Speaker;
            }

            sb.AppendLine($"[{segment.StartTime:hh\\:mm\\:ss} - {segment.EndTime:hh\\:mm\\:ss}] {segment.Text}");
        }

        return sb.ToString();
    }

    private void LoadSettings()
    {
        try
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RecordingsHelper",
                SettingsFileName);

            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize<TranscriptionSettings>(json);
                
                if (settings != null)
                {
                    Settings = settings;
                }
            }
        }
        catch
        {
            // Ignore settings load errors
        }
    }

    private void SaveSettings()
    {
        try
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RecordingsHelper",
                SettingsFileName);

            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);

            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(settingsPath, json);
        }
        catch
        {
            // Ignore settings save errors
        }
    }

    public void Cleanup()
    {
        // Reset state to initial values
        Items.Clear();
        IsSubmitting = false;
    }
}
