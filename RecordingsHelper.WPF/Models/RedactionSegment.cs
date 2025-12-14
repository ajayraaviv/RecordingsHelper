using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RecordingsHelper.WPF.Models;

public partial class RedactionSegment : ObservableObject
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string StartTimeFormatted => FormatTimeSpan(StartTime);
    public string EndTimeFormatted => FormatTimeSpan(EndTime);
    public TimeSpan Duration => EndTime - StartTime;
    public string DurationFormatted => FormatTimeSpan(Duration);
    public bool IsValid => EndTime > StartTime && StartTime >= TimeSpan.Zero;
    
    [ObservableProperty]
    private bool _useGlobalAction = true;
    
    [ObservableProperty]
    private bool _muteSegment = false;
    
    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return ts.ToString(@"hh\:mm\:ss\.fff");
        return ts.ToString(@"mm\:ss\.fff");
    }
}
