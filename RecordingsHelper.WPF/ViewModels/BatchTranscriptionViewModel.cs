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
    private string _batchTranscriptionId = string.Empty;

    [ObservableProperty]
    private bool _enableDiarization = true;

    [ObservableProperty]
    private int _maxSpeakers = 6;

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
            BatchTranscriptionId = string.Empty;
        }
    }

    [RelayCommand]
    private void CopyBatchId()
    {
        if (!string.IsNullOrWhiteSpace(BatchTranscriptionId))
        {
            try
            {
                Clipboard.SetText(BatchTranscriptionId);
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

        IsSubmitting = true;

        try
        {
            // Upload all files to blob storage and collect their SAS URLs
            var blobUrls = new List<string>();
            var uploadFolder = $"batch-{DateTime.UtcNow:yyyyMMddHHmmss}";

            foreach (var item in notStartedItems)
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

                blobUrls.Add(blobUrl);
                item.BlobUrl = blobUrl;
                item.StatusMessage = "Uploaded to storage";
            }

            // Create a single batch transcription for all files using individual blob URLs with SAS tokens
            var options = new TranscriptionOptions
            {
                Mode = TranscriptionMode.Batch,
                EnableDiarization = EnableDiarization,
                MaxSpeakers = MaxSpeakers
            };

            BatchTranscriptionId = await _transcriptionService.CreateBatchTranscriptionWithUrlsAsync(
                blobUrls, // Individual blob URLs with SAS tokens
                Settings,
                options,
                CancellationToken.None);

            // Update all items with the batch transcription ID
            foreach (var item in notStartedItems)
            {
                item.TranscriptionId = BatchTranscriptionId;
                item.Status = BatchTranscriptionStatus.Submitted;
                item.StatusMessage = "Submitted";
                item.SubmittedAt = DateTime.Now;
            }

            MessageBox.Show($"Successfully submitted batch with {notStartedItems.Count} file(s) for transcription.\nBatch ID: {BatchTranscriptionId}", "Success",
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
        if (string.IsNullOrWhiteSpace(BatchTranscriptionId))
        {
            MessageBox.Show("No batch transcription ID found. Please submit files first.", "Error",
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
            var status = await _transcriptionService.GetBatchTranscriptionStatusAsync(
                Settings.Region!,
                Settings.SubscriptionKey!,
                BatchTranscriptionId,
                CancellationToken.None);

            // If succeeded, get the detailed report to update individual file statuses
            if (status.Status == BatchTranscriptionStatus.Succeeded)
            {
                var report = await _transcriptionService.GetBatchTranscriptionReportAsync(
                    Settings.Region!,
                    Settings.SubscriptionKey!,
                    BatchTranscriptionId,
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
                            i.TranscriptionId == BatchTranscriptionId && 
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
                            }
                        }
                    }

                    var successCount = report.RootElement.GetProperty("successfulTranscriptionsCount").GetInt32();
                    var failedCount = report.RootElement.GetProperty("failedTranscriptionsCount").GetInt32();

                    if (failedCount > 0)
                    {
                        MessageBox.Show($"Batch transcription completed with errors!\n\nSuccessful: {successCount}\nFailed: {failedCount}\n\nCheck the Error Message column for details.", "Completed with Errors",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        MessageBox.Show($"Batch transcription completed successfully!\n\nSuccessful: {successCount}", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    // Fallback: update all items with the same status
                    foreach (var item in Items.Where(i => i.TranscriptionId == BatchTranscriptionId))
                    {
                        item.Status = status.Status;
                        item.StatusMessage = status.StatusMessage;
                        item.CompletedAt = DateTime.Now;
                    }

                    MessageBox.Show("Batch transcription completed successfully!", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else if (status.Status == BatchTranscriptionStatus.Failed)
            {
                // Update all items
                foreach (var item in Items.Where(i => i.TranscriptionId == BatchTranscriptionId))
                {
                    item.Status = status.Status;
                    item.StatusMessage = status.StatusMessage;
                    item.ErrorMessage = status.ErrorMessage;
                }

                var errorDetails = new StringBuilder();
                errorDetails.AppendLine($"Batch ID: {BatchTranscriptionId}");
                errorDetails.AppendLine($"Status: {status.StatusMessage}");
                errorDetails.AppendLine($"Error: {status.ErrorMessage ?? "Unknown error"}");
                
                MessageBox.Show(errorDetails.ToString(), "Batch Transcription Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                // Update all items with running/submitted status
                foreach (var item in Items.Where(i => i.TranscriptionId == BatchTranscriptionId))
                {
                    item.Status = status.Status;
                    item.StatusMessage = status.StatusMessage;
                }

                MessageBox.Show($"Batch status: {status.StatusMessage}\nBatch ID: {BatchTranscriptionId}", "Status",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error checking status for Batch ID {BatchTranscriptionId}:\n{ex.Message}\n\nStack trace:\n{ex.StackTrace}", "Error",
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
                Filter = "Text Files (*.txt)|*.txt|JSON Files (*.json)|*.json",
                FileName = $"{Path.GetFileNameWithoutExtension(item.FilePath)}_transcript",
                Title = "Save Transcript"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                var extension = Path.GetExtension(saveFileDialog.FileName).ToLowerInvariant();
                
                if (extension == ".json")
                {
                    var json = JsonSerializer.Serialize(item.Segments, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    await File.WriteAllTextAsync(saveFileDialog.FileName, json);
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
}
