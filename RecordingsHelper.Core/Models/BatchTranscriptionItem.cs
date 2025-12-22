using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RecordingsHelper.Core.Models;

public class BatchTranscriptionItem : INotifyPropertyChanged
{
    private string _filePath = string.Empty;
    private string _transcriptionId = string.Empty;
    private string _blobUrl = string.Empty;
    private BatchTranscriptionStatus _status = BatchTranscriptionStatus.NotStarted;
    private string _statusMessage = "Ready to submit";
    private DateTime? _submittedAt;
    private DateTime? _completedAt;
    private List<TranscriptionSegment> _segments = new();
    private string? _errorMessage;

    public string FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath != value)
            {
                _filePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FileName));
            }
        }
    }

    public string FileName => System.IO.Path.GetFileName(FilePath);

    public string TranscriptionId
    {
        get => _transcriptionId;
        set
        {
            if (_transcriptionId != value)
            {
                _transcriptionId = value;
                OnPropertyChanged();
            }
        }
    }

    public string BlobUrl
    {
        get => _blobUrl;
        set
        {
            if (_blobUrl != value)
            {
                _blobUrl = value;
                OnPropertyChanged();
            }
        }
    }

    public BatchTranscriptionStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public DateTime? SubmittedAt
    {
        get => _submittedAt;
        set
        {
            if (_submittedAt != value)
            {
                _submittedAt = value;
                OnPropertyChanged();
            }
        }
    }

    public DateTime? CompletedAt
    {
        get => _completedAt;
        set
        {
            if (_completedAt != value)
            {
                _completedAt = value;
                OnPropertyChanged();
            }
        }
    }

    public List<TranscriptionSegment> Segments
    {
        get => _segments;
        set
        {
            if (_segments != value)
            {
                _segments = value;
                OnPropertyChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public enum BatchTranscriptionStatus
{
    NotStarted,
    Uploading,
    Submitted,
    Running,
    Succeeded,
    Failed
}
