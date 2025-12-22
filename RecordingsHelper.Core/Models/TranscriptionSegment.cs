namespace RecordingsHelper.Core.Models;

public class TranscriptionSegment
{
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Speaker { get; set; } = "Unknown";
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
}
