namespace RecordingsHelper.Core.Models;

/// <summary>
/// Represents a time segment in an audio file
/// </summary>
public class AudioSegment
{
    /// <summary>
    /// Start time of the segment
    /// </summary>
    public TimeSpan Start { get; set; }

    /// <summary>
    /// End time of the segment
    /// </summary>
    public TimeSpan End { get; set; }

    /// <summary>
    /// Duration of the segment
    /// </summary>
    public TimeSpan Duration => End - Start;

    /// <summary>
    /// Validates the segment
    /// </summary>
    public bool IsValid => End > Start && Start >= TimeSpan.Zero;

    public AudioSegment(TimeSpan start, TimeSpan end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Creates a segment from start time and duration
    /// </summary>
    public static AudioSegment FromDuration(TimeSpan start, TimeSpan duration)
    {
        return new AudioSegment(start, start + duration);
    }

    /// <summary>
    /// Creates a segment from seconds
    /// </summary>
    public static AudioSegment FromSeconds(double startSeconds, double endSeconds)
    {
        return new AudioSegment(
            TimeSpan.FromSeconds(startSeconds),
            TimeSpan.FromSeconds(endSeconds)
        );
    }

    public override string ToString()
    {
        return $"{Start:mm\\:ss\\.fff} - {End:mm\\:ss\\.fff} ({Duration:mm\\:ss\\.fff})";
    }
}
