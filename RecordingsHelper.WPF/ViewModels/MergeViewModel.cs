using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using NAudio.Wave;
using RecordingsHelper.Core.Services;
using RecordingsHelper.WPF.Models;

namespace RecordingsHelper.WPF.ViewModels;

public partial class MergeViewModel : ObservableObject
{
    private readonly AudioStitcher _audioStitcher;

    [ObservableProperty]
    private ObservableCollection<AudioFileItem> _audioFiles = new();

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _normalizeAudio = true;

    [ObservableProperty]
    private double _crossfadeDuration = 0.5;

    [ObservableProperty]
    private TimeSpan _totalDuration;

    [ObservableProperty]
    private string _totalDurationFormatted = "00:00.000";

    public MergeViewModel()
    {
        _audioStitcher = new AudioStitcher();
    }

    [RelayCommand]
    private void AddFiles()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Audio Files|*.wav;*.mp3;*.ogg;*.mp4;*.m4a;*.aac;*.wma;*.aiff|All Files|*.*",
            Multiselect = true,
            Title = "Select Audio Files to Merge"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            foreach (var filePath in openFileDialog.FileNames)
            {
                AddAudioFile(filePath);
            }
        }
    }

    private void AddAudioFile(string filePath)
    {
        try
        {
            TimeSpan duration;
            using (var reader = new AudioFileReader(filePath))
            {
                duration = reader.TotalTime;
            }

            var fileItem = new AudioFileItem
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                Duration = duration,
                Format = Path.GetExtension(filePath).TrimStart('.').ToUpper(),
                Order = AudioFiles.Count + 1
            };

            AudioFiles.Add(fileItem);
            UpdateTimeline();
            StatusMessage = $"Added {fileItem.FileName}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void MoveUp(AudioFileItem item)
    {
        var index = AudioFiles.IndexOf(item);
        if (index > 0)
        {
            AudioFiles.Move(index, index - 1);
            UpdateOrder();
        }
    }

    [RelayCommand]
    private void MoveDown(AudioFileItem item)
    {
        var index = AudioFiles.IndexOf(item);
        if (index < AudioFiles.Count - 1)
        {
            AudioFiles.Move(index, index + 1);
            UpdateOrder();
        }
    }

    [RelayCommand]
    private void Remove(AudioFileItem item)
    {
        AudioFiles.Remove(item);
        UpdateOrder();
        StatusMessage = $"Removed {item.FileName}";
    }

    [RelayCommand]
    private void Clear()
    {
        AudioFiles.Clear();
        StatusMessage = "Cleared all files";
    }

    private void UpdateOrder()
    {
        for (int i = 0; i < AudioFiles.Count; i++)
        {
            AudioFiles[i].Order = i + 1;
        }
        UpdateTimeline();
    }

    private void UpdateTimeline()
    {
        TimeSpan currentPosition = TimeSpan.Zero;
        
        foreach (var file in AudioFiles)
        {
            file.StartTime = currentPosition;
            file.EndTime = currentPosition + file.Duration;
            currentPosition = file.EndTime;
        }
        
        TotalDuration = currentPosition;
        TotalDurationFormatted = TotalDuration.ToString(@"mm\:ss\.fff");
    }

    [RelayCommand]
    private async Task MergeFiles()
    {
        if (AudioFiles.Count < 2)
        {
            MessageBox.Show("Please add at least 2 audio files to merge.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var saveFileDialog = new SaveFileDialog
        {
            Filter = "WAV Files|*.wav",
            Title = "Save Merged Audio File",
            FileName = "merged_audio.wav"
        };

        if (saveFileDialog.ShowDialog() != true)
            return;

        IsProcessing = true;
        StatusMessage = "Merging audio files...";

        try
        {
            await Task.Run(() =>
            {
                var filePaths = AudioFiles.Select(f => f.FilePath).ToArray();

                if (NormalizeAudio && CrossfadeDuration > 0)
                {
                    _audioStitcher.StitchWithNormalization(filePaths, saveFileDialog.FileName, (float)CrossfadeDuration);
                }
                else if (NormalizeAudio)
                {
                    _audioStitcher.StitchWithNormalization(filePaths, saveFileDialog.FileName);
                }
                else
                {
                    _audioStitcher.StitchAudioFiles(filePaths, saveFileDialog.FileName);
                }
            });

            StatusMessage = $"Successfully merged {AudioFiles.Count} files!";
            MessageBox.Show($"Audio files merged successfully!\n\nSaved to: {saveFileDialog.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            
            // Reset state after successful merge
            ResetState();
        }
        catch (Exception ex)
        {
            StatusMessage = "Error during merge";
            MessageBox.Show($"Error merging files: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
        }
    }
    
    private void ResetState()
    {
        // Clear all files
        AudioFiles.Clear();
        
        // Reset settings to defaults
        NormalizeAudio = false;
        CrossfadeDuration = 0.0;
        
        // Clear calculated properties
        TotalDuration = TimeSpan.Zero;
        TotalDurationFormatted = "00:00.000";
        
        StatusMessage = "Ready to merge new files";
    }
}
