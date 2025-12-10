# Example Usage Scenarios

This document provides detailed examples for common use cases of the **RecordingsHelper.Core** library.

## Scenario 1: Podcast Production

Combine intro music, interview segments, and outro music with silence gaps.

```csharp
using RecordingsHelper.Core.Services;
using RecordingsHelper.Core.Extensions;

var stitcher = new AudioStitcher();

// Convert intro/outro music from MP3 to match WAV recordings
"intro_music.mp3".ConvertToWav("intro.wav");
"outro_music.mp3".ConvertToWav("outro.wav");

// Combine all segments with 1 second silence between parts
var podcastSegments = new[]
{
    "intro.wav",
    "interview_part1.wav",
    "interview_part2.wav",
    "interview_part3.wav",
    "outro.wav"
};

stitcher.StitchAudioFiles(podcastSegments, "final_podcast.wav", insertSilence: 1000);

// Check total duration
var duration = stitcher.GetTotalDuration(podcastSegments);
Console.WriteLine($"Final podcast duration: {duration}");
```

## Scenario 2: Music Playlist Creation

Create a continuous mix from multiple songs.

```csharp
using RecordingsHelper.Core.Services;

var stitcher = new AudioStitcher();

var playlist = new[]
{
    "song1.mp3",
    "song2.ogg",
    "song3.wav",
    "song4.m4a"
};

// Stitch without gaps for continuous playback
stitcher.StitchAudioFiles(playlist, "party_mix.wav");

Console.WriteLine("Party mix created successfully!");
```

## Scenario 3: Voice Recording Compilation

Combine multiple voice recordings with normalization.

```csharp
using RecordingsHelper.Core.Services;

var stitcher = new AudioStitcher();

var recordings = new[]
{
    "chapter1.wav",
    "chapter2.wav",
    "chapter3.wav",
    "chapter4.wav"
};

// Normalize to ensure consistent volume throughout
stitcher.StitchWithNormalization(recordings, "audiobook.wav", targetPeak: 0.90f);

Console.WriteLine("Audiobook compilation complete!");
```

## Scenario 4: Format Conversion Batch Processing

Convert multiple audio files from various formats to WAV.

```csharp
using RecordingsHelper.Core.Extensions;

var filesToConvert = new[]
{
    "audio1.mp3",
    "audio2.ogg",
    "audio3.m4a",
    "audio4.wma"
};

foreach (var file in filesToConvert)
{
    if (file.IsSupportedAudioFormat())
    {
        var outputFile = Path.ChangeExtension(file, ".wav");
        file.ConvertToWav(outputFile);
        Console.WriteLine($"Converted: {file} -> {outputFile}");
    }
}
```

## Scenario 5: Lecture Recording Assembly

Combine lecture segments with announcements.

```csharp
using RecordingsHelper.Core.Services;
using RecordingsHelper.Core.Extensions;

var stitcher = new AudioStitcher();

// Convert announcement from MP3
"announcement.mp3".ConvertToWav("announcement_temp.wav");

var lectureFiles = new[]
{
    "announcement_temp.wav",
    "lecture_intro.wav",
    "lecture_main.wav",
    "lecture_qa.wav"
};

// Add 2 seconds between segments for clarity
stitcher.StitchAudioFiles(lectureFiles, "complete_lecture.wav", insertSilence: 2000);

// Clean up temporary file
File.Delete("announcement_temp.wav");

Console.WriteLine("Lecture recording assembled!");
```

## Scenario 6: Sound Effect Library Creation

Build a sound effects compilation.

```csharp
using RecordingsHelper.Core.Services;

var stitcher = new AudioStitcher();

var soundEffects = Directory.GetFiles("sound_effects", "*.wav");

// Add 500ms silence between each effect
stitcher.StitchWavFiles(soundEffects, "sfx_library.wav", insertSilence: 500);

Console.WriteLine($"Created SFX library with {soundEffects.Length} effects!");
```

## Scenario 7: Multi-Language Audio Assembly

Combine audio tracks from different sources.

```csharp
using RecordingsHelper.Core.Services;
using RecordingsHelper.Core.Extensions;

var stitcher = new AudioStitcher();

// Convert various formats to WAV
"english_part.mp3".ConvertToWav("english.wav");
"spanish_part.ogg".ConvertToWav("spanish.wav");
"french_part.m4a".ConvertToWav("french.wav");

var segments = new[] { "english.wav", "spanish.wav", "french.wav" };

// Normalize for consistent volume
stitcher.StitchWithNormalization(segments, "multilingual_guide.wav");

// Cleanup
foreach (var file in segments)
{
    File.Delete(file);
}
```

## Scenario 8: Analyzing Audio Before Stitching

Inspect audio properties before combining.

```csharp
using RecordingsHelper.Core.Extensions;

var files = new[] { "audio1.mp3", "audio2.wav", "audio3.ogg" };

foreach (var file in files)
{
    using var stream = file.ToWaveStream();
    Console.WriteLine($"\nFile: {file}");
    Console.WriteLine($"  Duration: {stream.TotalTime}");
    Console.WriteLine($"  Sample Rate: {stream.WaveFormat.SampleRate}Hz");
    Console.WriteLine($"  Channels: {stream.WaveFormat.Channels}");
    Console.WriteLine($"  Bits per Sample: {stream.WaveFormat.BitsPerSample}");
}

// Now proceed with stitching knowing the properties
```

## Scenario 9: Creating Training Materials

Combine instruction audio with practice exercises.

```csharp
using RecordingsHelper.Core.Services;

var stitcher = new AudioStitcher();

var trainingMaterial = new[]
{
    "introduction.wav",
    "lesson1.wav",
    "exercise1.wav",
    "lesson2.wav",
    "exercise2.wav",
    "conclusion.wav"
};

// Add 3 seconds between lessons for note-taking time
stitcher.StitchAudioFiles(trainingMaterial, "training_course.wav", insertSilence: 3000);

var totalTime = stitcher.GetTotalDuration(trainingMaterial);
Console.WriteLine($"Training course total time: {totalTime.TotalMinutes:F2} minutes");
```

## Scenario 10: Emergency Broadcast System

Quick assembly of alert messages.

```csharp
using RecordingsHelper.Core.Services;

var stitcher = new AudioStitcher();

// Repeat alert tone 3 times with the message
var alertSegments = new[]
{
    "alert_tone.wav",
    "alert_tone.wav",
    "alert_tone.wav",
    "emergency_message.wav",
    "alert_tone.wav"
};

stitcher.StitchWavFiles(alertSegments, "emergency_broadcast.wav", insertSilence: 500);

Console.WriteLine("Emergency broadcast ready!");
```

## Tips and Best Practices

1. **Pre-convert files**: If working with many files of the same non-WAV format, convert them all first for better performance.

2. **Check formats**: Use `IsSupportedAudioFormat()` before processing to avoid errors.

3. **Normalize when needed**: Use normalization when combining files from different sources or recording sessions.

4. **Calculate duration**: Always check total duration for time-sensitive projects.

5. **Cleanup**: Delete temporary converted files after stitching to save disk space.

6. **Batch operations**: Process similar files together for efficiency.

7. **Backup originals**: Keep original files before any conversion or stitching operation.

8. **Test output**: Always verify the output file plays correctly and meets quality expectations.
