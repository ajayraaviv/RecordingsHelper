using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RecordingsHelper.Core.Models;

public class TranscriptionSegment : INotifyPropertyChanged
{
    private string _speaker = "Unknown";
    private string _text = string.Empty;
    private string? _originalSpeaker;

    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    
    public string Speaker
    {
        get => _speaker;
        set
        {
            if (_speaker != value)
            {
                _speaker = value;
                OnPropertyChanged();
            }
        }
    }
    
    public string? OriginalSpeaker
    {
        get => _originalSpeaker;
        set
        {
            if (_originalSpeaker != value)
            {
                _originalSpeaker = value;
                OnPropertyChanged();
            }
        }
    }
    
    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value;
                OnPropertyChanged();
            }
        }
    }
    
    public double Confidence { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
