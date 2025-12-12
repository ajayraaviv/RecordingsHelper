using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RecordingsHelper.WPF.Models;

public class AudioFileItem : INotifyPropertyChanged
{
    private int _order;
    private TimeSpan _startTime;
    private TimeSpan _endTime;

    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string DurationFormatted => FormatTimeSpan(Duration);
    public string Format { get; set; } = string.Empty;
    
    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return ts.ToString(@"hh\:mm\:ss\.fff");
        return ts.ToString(@"mm\:ss\.fff");
    }
    
    public int Order
    {
        get => _order;
        set
        {
            if (_order != value)
            {
                _order = value;
                OnPropertyChanged();
            }
        }
    }
    
    // Calculated properties for merge timeline
    public TimeSpan StartTime
    {
        get => _startTime;
        set
        {
            if (_startTime != value)
            {
                _startTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StartTimeFormatted));
                OnPropertyChanged(nameof(TimelineInfo));
            }
        }
    }
    
    public TimeSpan EndTime
    {
        get => _endTime;
        set
        {
            if (_endTime != value)
            {
                _endTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EndTimeFormatted));
                OnPropertyChanged(nameof(TimelineInfo));
            }
        }
    }
    
    public string StartTimeFormatted => FormatTimeSpan(StartTime);
    public string EndTimeFormatted => FormatTimeSpan(EndTime);
    public string TimelineInfo => $"{StartTimeFormatted} → {EndTimeFormatted}";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
