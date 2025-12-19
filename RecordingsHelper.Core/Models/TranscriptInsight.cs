namespace RecordingsHelper.Core.Models;

public class TranscriptInsight
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public int SpeakerId { get; set; }
    public string Language { get; set; } = string.Empty;
    public List<TranscriptInstance> Instances { get; set; } = new();
}

public class TranscriptInstance
{
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public string AdjustedStart { get; set; } = string.Empty;
    public string AdjustedEnd { get; set; } = string.Empty;
    public double Confidence { get; set; }
    
    public TimeSpan StartTime => ParseTime(AdjustedStart);
    public TimeSpan EndTime => ParseTime(AdjustedEnd);
    
    private static TimeSpan ParseTime(string timeString)
    {
        if (TimeSpan.TryParse(timeString, out var result))
            return result;
        return TimeSpan.Zero;
    }
}

public class VideoInsights
{
    public List<TranscriptInsight> Transcript { get; set; } = new();
}

public class InsightsRoot
{
    public List<VideoData> Videos { get; set; } = new();
}

public class VideoData
{
    public VideoInsights Insights { get; set; } = new();
}
