using RecordingsHelper.Core.Extensions;
using RecordingsHelper.Core.Services;

namespace RecordingsHelper;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Audio Stitching Helper ===\n");

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

        Console.WriteLine("\n=== Quick Start ===");
        Console.WriteLine("1. Place your audio files in the same directory");
        Console.WriteLine("2. Create an instance: var stitcher = new AudioStitcher();");
        Console.WriteLine("3. Call stitch method with file paths and output filename");
        Console.WriteLine("4. Check the output file!");

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
