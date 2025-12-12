using System;

namespace RecordingsHelper.WPF.Models;

public class RedactionSegment
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string StartTimeFormatted => StartTime.ToString(@"mm\:ss\.fff");
    public string EndTimeFormatted => EndTime.ToString(@"mm\:ss\.fff");
    public TimeSpan Duration => EndTime - StartTime;
    public string DurationFormatted => Duration.ToString(@"mm\:ss\.fff");
    public bool IsValid => EndTime > StartTime && StartTime >= TimeSpan.Zero;
}
