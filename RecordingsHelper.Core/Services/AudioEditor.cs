using NAudio.Wave;
using RecordingsHelper.Core.Models;

namespace RecordingsHelper.Core.Services;

/// <summary>
/// Service for editing audio files - removing or muting segments
/// </summary>
public class AudioEditor
{
    /// <summary>
    /// Removes specified segments from an audio file
    /// Processes segments from right to left to maintain timeline accuracy
    /// </summary>
    /// <param name="inputFile">Path to the input audio file</param>
    /// <param name="outputFile">Path where the edited output will be saved</param>
    /// <param name="segmentsToRemove">List of time segments to remove</param>
    public void RemoveSegments(string inputFile, string outputFile, List<AudioSegment> segmentsToRemove)
    {
        if (!File.Exists(inputFile))
            throw new FileNotFoundException($"Input file not found: {inputFile}");

        if (segmentsToRemove == null || segmentsToRemove.Count == 0)
            throw new ArgumentException("At least one segment must be specified", nameof(segmentsToRemove));

        // Validate all segments
        foreach (var segment in segmentsToRemove)
        {
            if (!segment.IsValid)
                throw new ArgumentException($"Invalid segment: {segment}");
        }

        // Sort segments by start time in descending order (right to left processing)
        var sortedSegments = segmentsToRemove
            .OrderByDescending(s => s.Start)
            .ToList();

        using var reader = OpenAudioFile(inputFile);
        var format = reader.WaveFormat;
        
        // Validate segments are within file duration
        foreach (var segment in sortedSegments)
        {
            if (segment.End > reader.TotalTime)
                throw new ArgumentException(
                    $"Segment end time {segment.End} exceeds file duration {reader.TotalTime}");
        }

        // Check for overlapping segments
        ValidateNoOverlaps(sortedSegments);

        // Build list of segments to keep
        var segmentsToKeep = BuildKeepSegments(sortedSegments, reader.TotalTime);

        // Write output file with only the segments we want to keep
        using var writer = new WaveFileWriter(outputFile, format);
        
        foreach (var keepSegment in segmentsToKeep)
        {
            reader.Position = TimeToPosition(keepSegment.Start, format);
            var bytesToRead = (int)TimeToBytes(keepSegment.Duration, format);
            var buffer = new byte[format.AverageBytesPerSecond]; // 1 second buffer
            var totalBytesRead = 0;

            while (totalBytesRead < bytesToRead)
            {
                var bytesToReadNow = Math.Min(buffer.Length, bytesToRead - totalBytesRead);
                var bytesRead = reader.Read(buffer, 0, bytesToReadNow);
                
                if (bytesRead == 0)
                    break;

                writer.Write(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;
            }
        }
    }

    /// <summary>
    /// Mutes specified segments by replacing them with silence
    /// This preserves the timeline and file duration
    /// </summary>
    /// <param name="inputFile">Path to the input audio file</param>
    /// <param name="outputFile">Path where the edited output will be saved</param>
    /// <param name="segmentsToMute">List of time segments to mute</param>
    public void MuteSegments(string inputFile, string outputFile, List<AudioSegment> segmentsToMute)
    {
        if (!File.Exists(inputFile))
            throw new FileNotFoundException($"Input file not found: {inputFile}");

        if (segmentsToMute == null || segmentsToMute.Count == 0)
            throw new ArgumentException("At least one segment must be specified", nameof(segmentsToMute));

        // Validate all segments
        foreach (var segment in segmentsToMute)
        {
            if (!segment.IsValid)
                throw new ArgumentException($"Invalid segment: {segment}");
        }

        using var reader = OpenAudioFile(inputFile);
        var format = reader.WaveFormat;

        // Validate segments are within file duration
        foreach (var segment in segmentsToMute)
        {
            if (segment.End > reader.TotalTime)
                throw new ArgumentException(
                    $"Segment end time {segment.End} exceeds file duration {reader.TotalTime}");
        }

        // Sort segments by start time for efficient processing
        var sortedSegments = segmentsToMute.OrderBy(s => s.Start).ToList();

        using var writer = new WaveFileWriter(outputFile, format);
        
        var buffer = new byte[format.AverageBytesPerSecond]; // 1 second buffer
        var currentTime = TimeSpan.Zero;
        var segmentIndex = 0;

        while (reader.Position < reader.Length)
        {
            // Check if we're in a segment that should be muted
            var shouldMute = false;
            
            if (segmentIndex < sortedSegments.Count)
            {
                var currentSegment = sortedSegments[segmentIndex];
                
                if (currentTime >= currentSegment.Start && currentTime < currentSegment.End)
                {
                    shouldMute = true;
                }
                else if (currentTime >= currentSegment.End)
                {
                    segmentIndex++;
                    continue; // Recheck with next segment
                }
            }

            var bytesRead = reader.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0)
                break;

            if (shouldMute)
            {
                // Write silence (zeros)
                Array.Clear(buffer, 0, bytesRead);
            }

            writer.Write(buffer, 0, bytesRead);
            currentTime = TimeSpan.FromSeconds((double)reader.Position / format.AverageBytesPerSecond);
        }
    }

    /// <summary>
    /// Removes a single segment from an audio file
    /// </summary>
    public void RemoveSegment(string inputFile, string outputFile, TimeSpan start, TimeSpan end)
    {
        RemoveSegments(inputFile, outputFile, new List<AudioSegment> 
        { 
            new AudioSegment(start, end) 
        });
    }

    /// <summary>
    /// Mutes a single segment in an audio file
    /// </summary>
    public void MuteSegment(string inputFile, string outputFile, TimeSpan start, TimeSpan end)
    {
        MuteSegments(inputFile, outputFile, new List<AudioSegment> 
        { 
            new AudioSegment(start, end) 
        });
    }

    /// <summary>
    /// Removes segments specified in seconds
    /// </summary>
    public void RemoveSegmentsInSeconds(string inputFile, string outputFile, 
        List<(double start, double end)> segments)
    {
        var audioSegments = segments
            .Select(s => AudioSegment.FromSeconds(s.start, s.end))
            .ToList();
        
        RemoveSegments(inputFile, outputFile, audioSegments);
    }

    /// <summary>
    /// Mutes segments specified in seconds
    /// </summary>
    public void MuteSegmentsInSeconds(string inputFile, string outputFile, 
        List<(double start, double end)> segments)
    {
        var audioSegments = segments
            .Select(s => AudioSegment.FromSeconds(s.start, s.end))
            .ToList();
        
        MuteSegments(inputFile, outputFile, audioSegments);
    }

    /// <summary>
    /// Gets the duration of the file after segments are removed
    /// </summary>
    public TimeSpan GetDurationAfterRemoval(string inputFile, List<AudioSegment> segmentsToRemove)
    {
        if (!File.Exists(inputFile))
            throw new FileNotFoundException($"Input file not found: {inputFile}");

        using var reader = OpenAudioFile(inputFile);
        var originalDuration = reader.TotalTime;
        
        var totalRemovalTime = segmentsToRemove
            .Where(s => s.IsValid && s.End <= originalDuration)
            .Sum(s => s.Duration.TotalSeconds);

        return TimeSpan.FromSeconds(originalDuration.TotalSeconds - totalRemovalTime);
    }

    /// <summary>
    /// Builds list of segments to keep (inverse of segments to remove)
    /// </summary>
    private List<AudioSegment> BuildKeepSegments(List<AudioSegment> segmentsToRemove, TimeSpan totalDuration)
    {
        var keepSegments = new List<AudioSegment>();
        var sortedRemove = segmentsToRemove.OrderBy(s => s.Start).ToList();
        
        var currentTime = TimeSpan.Zero;

        foreach (var removeSegment in sortedRemove)
        {
            // Add the segment before this removal
            if (removeSegment.Start > currentTime)
            {
                keepSegments.Add(new AudioSegment(currentTime, removeSegment.Start));
            }
            
            currentTime = removeSegment.End;
        }

        // Add the final segment after the last removal
        if (currentTime < totalDuration)
        {
            keepSegments.Add(new AudioSegment(currentTime, totalDuration));
        }

        return keepSegments;
    }

    /// <summary>
    /// Validates that segments don't overlap
    /// </summary>
    private void ValidateNoOverlaps(List<AudioSegment> segments)
    {
        var sorted = segments.OrderBy(s => s.Start).ToList();
        
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (sorted[i].End > sorted[i + 1].Start)
            {
                throw new ArgumentException(
                    $"Overlapping segments detected: {sorted[i]} overlaps with {sorted[i + 1]}");
            }
        }
    }

    /// <summary>
    /// Opens an audio file and returns a WaveStream
    /// </summary>
    private WaveStream OpenAudioFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".mp3" => new Mp3FileReader(filePath),
            ".wav" => new WaveFileReader(filePath),
            ".aiff" or ".aif" => new AiffFileReader(filePath),
            _ => new MediaFoundationReader(filePath)
        };
    }

    /// <summary>
    /// Converts time to byte position in the stream
    /// </summary>
    private long TimeToPosition(TimeSpan time, WaveFormat format)
    {
        var bytes = TimeToBytes(time, format);
        // Ensure position is aligned to block boundary
        return (bytes / format.BlockAlign) * format.BlockAlign;
    }

    /// <summary>
    /// Converts time to number of bytes
    /// </summary>
    private long TimeToBytes(TimeSpan time, WaveFormat format)
    {
        return (long)(time.TotalSeconds * format.AverageBytesPerSecond);
    }
}
