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
            var startPosition = TimeToPosition(keepSegment.Start, format);
            var bytesToRead = (int)TimeToBytes(keepSegment.Duration, format);

            // Ensure we don't read beyond file length
            var maxPosition = reader.Length;
            if (startPosition >= maxPosition)
                continue;

            reader.Position = startPosition;

            // Adjust bytes to read if we would exceed file length
            var remainingBytes = maxPosition - startPosition;
            bytesToRead = (int)Math.Min(bytesToRead, remainingBytes);

            // Ensure bytesToRead is a multiple of BlockAlign
            bytesToRead = (bytesToRead / format.BlockAlign) * format.BlockAlign;
            if (bytesToRead <= 0)
                continue;

            // Use 100ms buffer for better performance and consistency with mute operations
            var buffer = new byte[format.AverageBytesPerSecond / 10]; // 100ms buffer
            var totalBytesRead = 0;

            while (totalBytesRead < bytesToRead)
            {
                var bytesToReadNow = Math.Min(buffer.Length, bytesToRead - totalBytesRead);
                var bytesRead = reader.Read(buffer, 0, bytesToReadNow);

                if (bytesRead == 0)
                {
                    // Unexpected end of stream
                    throw new InvalidOperationException(
                        $"Unexpected end of stream at position {reader.Position}. " +
                        $"Expected to read {bytesToRead} bytes but only read {totalBytesRead}");
                }

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
        
        // Use 100ms buffer for better precision while maintaining good performance
        var buffer = new byte[format.AverageBytesPerSecond / 10]; // 100ms buffer
        var currentTime = TimeSpan.Zero;
        var segmentIndex = 0;

        while (reader.Position < reader.Length)
        {
            var bytesRead = reader.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0)
                break;

            // Check if we're in a segment that should be muted
            var shouldMute = false;
            
            while (segmentIndex < sortedSegments.Count)
            {
                var currentSegment = sortedSegments[segmentIndex];
                
                if (currentTime >= currentSegment.End)
                {
                    // Move to next segment and recheck
                    segmentIndex++;
                    continue;
                }
                
                if (currentTime >= currentSegment.Start && currentTime < currentSegment.End)
                {
                    shouldMute = true;
                }
                
                break; // Exit the while loop after checking current segment
            }

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

        WaveStream sourceStream = extension switch
        {
            ".mp3" => new Mp3FileReader(filePath),
            ".wav" => new WaveFileReader(filePath),
            ".aiff" or ".aif" => new AiffFileReader(filePath),
            _ => new MediaFoundationReader(filePath)
        };

        // For MediaFoundationReader (MP4, M4A, etc.), convert to a wave stream
        // that supports better seeking by reading into memory
        if (sourceStream is MediaFoundationReader mfReader)
        {
            // Read the entire audio into a memory stream for reliable seeking
            var memoryStream = new MemoryStream();
            var waveWriter = new WaveFileWriter(memoryStream, mfReader.WaveFormat);
            
            var buffer = new byte[mfReader.WaveFormat.AverageBytesPerSecond * 4]; // 4 second buffer
            int bytesRead;
            while ((bytesRead = mfReader.Read(buffer, 0, buffer.Length)) > 0)
            {
                waveWriter.Write(buffer, 0, bytesRead);
            }
            
            waveWriter.Flush();
            // Don't dispose waveWriter, just dispose the source reader
            mfReader.Dispose();
            
            memoryStream.Position = 0;
            return new WaveFileReader(memoryStream);
        }

        return sourceStream;
    }

    /// <summary>
    /// Splits an audio file at specified split points into multiple output files
    /// </summary>
    /// <param name="inputFile">Path to the input audio file</param>
    /// <param name="outputDirectory">Directory where split files will be saved</param>
    /// <param name="outputFileNamePattern">Pattern for output filenames (e.g., "part_{0}.wav" where {0} is the part number)</param>
    /// <param name="splitPoints">List of TimeSpan values indicating where to split (must be sorted)</param>
    /// <returns>List of paths to the created split files</returns>
    public List<string> SplitAudioFile(string inputFile, string outputDirectory, string outputFileNamePattern, List<TimeSpan> splitPoints)
    {
        if (!File.Exists(inputFile))
            throw new FileNotFoundException($"Input file not found: {inputFile}");

        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("Output directory must be specified", nameof(outputDirectory));

        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        if (splitPoints == null || splitPoints.Count == 0)
            throw new ArgumentException("At least one split point must be specified", nameof(splitPoints));

        // Validate and sort split points
        var sortedSplitPoints = splitPoints.Distinct().OrderBy(t => t).ToList();
        
        using var reader = OpenAudioFile(inputFile);
        var format = reader.WaveFormat;
        var totalDuration = reader.TotalTime;

        // Validate all split points are within file duration
        foreach (var splitPoint in sortedSplitPoints)
        {
            if (splitPoint <= TimeSpan.Zero)
                throw new ArgumentException($"Split point must be greater than zero: {splitPoint}");
            
            if (splitPoint >= totalDuration)
                throw new ArgumentException($"Split point {splitPoint} exceeds file duration {totalDuration}");
        }

        // Build segments based on split points
        var segments = new List<AudioSegment>();
        var previousPoint = TimeSpan.Zero;

        foreach (var splitPoint in sortedSplitPoints)
        {
            segments.Add(AudioSegment.FromDuration(previousPoint, splitPoint - previousPoint));
            previousPoint = splitPoint;
        }

        // Add final segment from last split point to end
        segments.Add(AudioSegment.FromDuration(previousPoint, totalDuration - previousPoint));

        // Create output files
        var outputFiles = new List<string>();
        var buffer = new byte[format.AverageBytesPerSecond]; // 1 second buffer

        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var outputFileName = string.Format(outputFileNamePattern, i + 1);
            var outputFilePath = Path.Combine(outputDirectory, outputFileName);

            using var writer = new WaveFileWriter(outputFilePath, format);
            
            reader.Position = TimeToPosition(segment.Start, format);
            var bytesToRead = (int)TimeToBytes(segment.Duration, format);
            var totalBytesRead = 0;

            while (totalBytesRead < bytesToRead)
            {
                var bytesToReadNow = Math.Min(buffer.Length, bytesToRead - totalBytesRead);
                
                // Ensure we read complete blocks - align to BlockAlign
                bytesToReadNow = (bytesToReadNow / format.BlockAlign) * format.BlockAlign;
                
                if (bytesToReadNow == 0)
                    break;
                
                var bytesRead = reader.Read(buffer, 0, bytesToReadNow);
                
                if (bytesRead == 0)
                    break;

                writer.Write(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;
            }

            outputFiles.Add(outputFilePath);
        }

        return outputFiles;
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
