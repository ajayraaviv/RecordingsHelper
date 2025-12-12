using System;

namespace RecordingsHelper.WPF.Models;

public class RedactionSegment
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string StartTimeFormatted => FormatTimeSpan(StartTime);
    public string EndTimeFormatted => FormatTimeSpan(EndTime);
    public TimeSpan Duration => EndTime - StartTime;
    public string DurationFormatted => FormatTimeSpan(Duration);
    public bool IsValid => EndTime > StartTime && StartTime >= TimeSpan.Zero;
    
    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return ts.ToString(@"hh\:mm\:ss\.fff");
        return ts.ToString(@"mm\:ss\.fff");
    }
}
