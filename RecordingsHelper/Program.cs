using RecordingsHelper.Core.Extensions;
using RecordingsHelper.Core.Services;
using RecordingsHelper.Core.Models;

namespace RecordingsHelper;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Audio Processing Helper ===\n");

        // Example 1: Basic WAV file stitching
        Console.WriteLine("Example 1: Basic WAV File Stitching");
        Console.WriteLine("Usage: Stitch multiple WAV files together");
        Console.WriteLine("Code:");
        Console.WriteLine(@"
var stitcher = new AudioStitcher();
var wavFiles = new[] { ""audio1.wav"", ""audio2.wav"", ""audio3.wav"" };
stitcher.StitchWavFiles(wavFiles, ""output.wav"");
");

        // Example 2: Mixed format stitching
        Console.WriteLine("\nExample 2: Mixed Format Stitching");
        Console.WriteLine("Usage: Stitch audio files of different formats");
        Console.WriteLine("Code:");
        Console.WriteLine(@"
var stitcher = new AudioStitcher();
var mixedFiles = new[] { ""song.mp3"", ""voice.wav"", ""music.ogg"", ""audio.mp4"" };
stitcher.StitchAudioFiles(mixedFiles, ""combined.wav"");
");

        // Example 3: Stitching with silence between clips
        Console.WriteLine("\nExample 3: Stitching with Silence");
        Console.WriteLine("Usage: Add silence between audio segments");
        Console.WriteLine("Code:");
        Console.WriteLine(@"
var stitcher = new AudioStitcher();
var files = new[] { ""intro.wav"", ""main.wav"", ""outro.wav"" };
stitcher.StitchAudioFiles(files, ""podcast.wav"", insertSilence: 500); // 500ms silence
");

        // Example 4: Format conversion
        Console.WriteLine("\nExample 4: Audio Format Conversion");
        Console.WriteLine("Usage: Convert various audio formats to WAV");
        Console.WriteLine("Code:");
        Console.WriteLine(@"
// Convert MP3 to WAV
""music.mp3"".ConvertMp3ToWav(""music.wav"");

// Convert MP4 to WAV
""video.mp4"".ConvertMp4ToWav(""audio.wav"");

// Convert OGG to WAV
""sound.ogg"".ConvertOggToWav(""sound.wav"");

// Auto-detect and convert any supported format
""audio.m4a"".ConvertToWav(""converted.wav"");

// Check if a format is supported
if (""myfile.aac"".IsSupportedAudioFormat())
{
    ""myfile.aac"".ConvertToWav(""output.wav"");
}
");

        // Example 5: Stitching with normalization
        Console.WriteLine("\nExample 5: Normalized Stitching");
        Console.WriteLine("Usage: Balance audio levels when combining files");
        Console.WriteLine("Code:");
        Console.WriteLine(@"
var stitcher = new AudioStitcher();
var files = new[] { ""quiet.wav"", ""loud.wav"", ""medium.wav"" };
stitcher.StitchWithNormalization(files, ""balanced.wav"", targetPeak: 0.95f);
");

        // Example 6: Get total duration
        Console.WriteLine("\nExample 6: Calculate Total Duration");
        Console.WriteLine("Usage: Get combined duration of multiple files");
        Console.WriteLine("Code:");
        Console.WriteLine(@"
var stitcher = new AudioStitcher();
var files = new[] { ""part1.wav"", ""part2.mp3"", ""part3.ogg"" };
var duration = stitcher.GetTotalDuration(files);
Console.WriteLine($""Total duration: {duration}"");
");

        // Example 7: Working with streams
        Console.WriteLine("\nExample 7: Working with WaveStreams");
        Console.WriteLine("Usage: Get audio as a stream for further processing");
        Console.WriteLine("Code:");
        Console.WriteLine(@"
using var stream = ""audio.mp3"".ToWaveStream();
// Process the stream as needed
Console.WriteLine($""Sample Rate: {stream.WaveFormat.SampleRate}Hz"");
Console.WriteLine($""Channels: {stream.WaveFormat.Channels}"");
Console.WriteLine($""Duration: {stream.TotalTime}"");
");

        // Example 8: Remove segments from audio
        Console.WriteLine("\nExample 8: Remove Unwanted Segments");
        Console.WriteLine("Usage: Strip out sections from audio file");
        Console.WriteLine("Code:");
        Console.WriteLine(@"
var editor = new AudioEditor();

// Remove single segment (e.g., remove 10-15 seconds)
editor.RemoveSegment(""audio.wav"", ""edited.wav"", 
    TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15));

// Remove multiple segments (processed right-to-left automatically)
var segmentsToRemove = new List<AudioSegment>
{
    AudioSegment.FromSeconds(5, 10),    // Remove 5-10 seconds
    AudioSegment.FromSeconds(30, 35),   // Remove 30-35 seconds
    AudioSegment.FromSeconds(60, 70)    // Remove 60-70 seconds
};
editor.RemoveSegments(""podcast.wav"", ""clean.wav"", segmentsToRemove);

// Check new duration
var newDuration = editor.GetDurationAfterRemoval(""audio.wav"", segmentsToRemove);
Console.WriteLine($""New duration: {newDuration}"");
");

        // Example 9: Mute segments (replace with silence)
        Console.WriteLine("\nExample 9: Mute Segments (Preserve Timeline)");
        Console.WriteLine("Usage: Replace sections with silence, keeping original duration");
        Console.WriteLine("Code:");
        Console.WriteLine(@"
var editor = new AudioEditor();

// Mute single segment (e.g., censor profanity)
editor.MuteSegment(""interview.wav"", ""censored.wav"",
    TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(48));

// Mute multiple segments (timeline stays intact)
var segmentsToMute = new List<AudioSegment>
{
    AudioSegment.FromSeconds(12.5, 15.2),  // Mute 12.5-15.2 seconds
    AudioSegment.FromSeconds(67.3, 71.8),  // Mute 67.3-71.8 seconds
    new AudioSegment(
        TimeSpan.FromMinutes(2),           // Mute 2:00-2:30
        TimeSpan.FromMinutes(2).Add(TimeSpan.FromSeconds(30))
    )
};
editor.MuteSegments(""recording.wav"", ""muted.wav"", segmentsToMute);

// Using helper method with tuples
editor.MuteSegmentsInSeconds(""audio.mp3"", ""output.wav"", 
    new List<(double, double)> { (10.5, 15.3), (45.2, 50.8) });
");

        Console.WriteLine("\n=== Supported Formats ===");
        Console.WriteLine("Input: MP3, WAV, OGG, MP4, M4A, AAC, WMA, AIFF, AIF");
        Console.WriteLine("Output: WAV (16-bit PCM)");

        Console.WriteLine("\n=== Key Features ===");
        Console.WriteLine("✓ Stitch multiple audio files together");
        Console.WriteLine("✓ Support for mixed audio formats");
        Console.WriteLine("✓ Add silence between segments");
        Console.WriteLine("✓ Normalize audio levels");
        Console.WriteLine("✓ Automatic format conversion");
        Console.WriteLine("✓ High-quality resampling");
        Console.WriteLine("✓ Calculate total duration");
        Console.WriteLine("✓ Remove unwanted segments");
        Console.WriteLine("✓ Mute/censor specific time ranges");

        Console.WriteLine("\n=== Editing Best Practices ===");
        Console.WriteLine("• REMOVE segments: Shortens file, changes timeline");
        Console.WriteLine("  - Best for: Cutting out dead air, mistakes, unwanted content");
        Console.WriteLine("  - Processes right-to-left to maintain accuracy");
        Console.WriteLine("• MUTE segments: Preserves duration and timeline");
        Console.WriteLine("  - Best for: Censoring profanity, bleeping sensitive info");
        Console.WriteLine("  - Easier for syncing with video or transcripts");

        Console.WriteLine("\n=== Quick Start ===");
        Console.WriteLine("1. Place your audio files in the same directory");
        Console.WriteLine("2. Create instances: var stitcher = new AudioStitcher(); var editor = new AudioEditor();");
        Console.WriteLine("3. Call methods with file paths and parameters");
        Console.WriteLine("4. Check the output files!");

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
