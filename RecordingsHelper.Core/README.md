# RecordingsHelper.Core

A reusable .NET class library for audio file manipulation, specializing in stitching multiple audio files together and format conversion using NAudio.

## Overview

**RecordingsHelper.Core** is a standalone class library that provides comprehensive audio processing capabilities. It can be used in any .NET project including console applications, web APIs, desktop applications, and more.

## Features

- ✅ **Stitch multiple audio files** together seamlessly
- ✅ **Support for mixed formats** - combine MP3, WAV, OGG, MP4, AAC, WMA, AIFF files
- ✅ **Format conversion** - convert any supported format to WAV
- ✅ **Add silence** between audio segments
- ✅ **Audio normalization** - balance volume levels across files
- ✅ **High-quality resampling** - automatic format matching
- ✅ **Duration calculation** - get total playback time
- ✅ **Stream support** - work with WaveStreams directly
- ✅ **Remove segments** - strip out unwanted sections from audio
- ✅ **Mute segments** - replace sections with silence while preserving timeline

## Supported Formats

### Input Formats
- MP3 (.mp3)
- WAV (.wav)
- OGG Vorbis (.ogg)
- MP4/M4A (.mp4, .m4a)
- AAC (.aac)
- WMA (.wma)
- AIFF (.aiff, .aif)

### Output Format
- WAV (16-bit PCM)

## Installation

### Using Project Reference

Add a reference to the `RecordingsHelper.Core.csproj` in your project:

```bash
dotnet add reference path/to/RecordingsHelper.Core/RecordingsHelper.Core.csproj
```

### NuGet Package Dependencies

The library requires:
- NAudio (2.2.1+)
- NAudio.Vorbis (1.5.0+)

These are automatically included when you reference the project.

## Usage

### Basic Stitching

```csharp
using RecordingsHelper.Core.Services;

var stitcher = new AudioStitcher();
var files = new[] { "audio1.wav", "audio2.mp3", "audio3.ogg" };
stitcher.StitchAudioFiles(files, "output.wav");
```

### Format Conversion

```csharp
using RecordingsHelper.Core.Extensions;

// Convert MP3 to WAV
"music.mp3".ConvertToWav("music.wav");

// Check format support
if ("myfile.aac".IsSupportedAudioFormat())
{
    "myfile.aac".ConvertToWav("output.wav");
}
```

### Advanced Stitching

```csharp
using RecordingsHelper.Core.Services;

var stitcher = new AudioStitcher();

// Add silence between clips
var files = new[] { "intro.wav", "main.wav", "outro.wav" };
stitcher.StitchAudioFiles(files, "podcast.wav", insertSilence: 1000);

// Normalize audio levels
stitcher.StitchWithNormalization(files, "normalized.wav", targetPeak: 0.95f);

// Get total duration
var duration = stitcher.GetTotalDuration(files);
Console.WriteLine($"Total: {duration}");
```

### Audio Editing

```csharp
using RecordingsHelper.Core.Services;
using RecordingsHelper.Core.Models;

var editor = new AudioEditor();

// Remove unwanted segments (shortens file, changes timeline)
var segmentsToRemove = new List<AudioSegment>
{
    AudioSegment.FromSeconds(10, 15),   // Remove 10-15 seconds
    AudioSegment.FromSeconds(45, 52),   // Remove 45-52 seconds
};
editor.RemoveSegments("podcast.wav", "edited.wav", segmentsToRemove);

// Mute segments (preserves duration and timeline)
var segmentsToMute = new List<AudioSegment>
{
    AudioSegment.FromSeconds(30, 33),   // Mute 30-33 seconds
};
editor.MuteSegments("interview.wav", "censored.wav", segmentsToMute);

// Check new duration after removal
var newDuration = editor.GetDurationAfterRemoval("podcast.wav", segmentsToRemove);
Console.WriteLine($"New duration: {newDuration}");
```

## API Reference

### Namespaces

- `RecordingsHelper.Core.Services` - Audio stitching and editing services
- `RecordingsHelper.Core.Extensions` - Format conversion extensions
- `RecordingsHelper.Core.Models` - Data models for audio segments

### AudioStitcher Class

Located in `RecordingsHelper.Core.Services`

#### Methods

**StitchWavFiles**
```csharp
void StitchWavFiles(string[] inputFiles, string outputFile, int insertSilence = 0)
```
Stitches multiple WAV files together.

**StitchAudioFiles**
```csharp
void StitchAudioFiles(string[] inputFiles, string outputFile, int insertSilence = 0)
```
Stitches audio files of any supported format together.

**StitchWithNormalization**
```csharp
void StitchWithNormalization(string[] inputFiles, string outputFile, float targetPeak = 0.95f)
```
Stitches files with volume normalization.

**StitchWithCrossfade**
```csharp
void StitchWithCrossfade(string[] inputFiles, string outputFile, int crossfadeDuration = 1000)
```
Stitches files with crossfade transitions.

**GetTotalDuration**
```csharp
TimeSpan GetTotalDuration(string[] inputFiles)
```
Calculates the total duration of all input files.

### AudioConverterExtensions Class

Located in `RecordingsHelper.Core.Extensions`

#### Extension Methods

**ConvertMp3ToWav**
```csharp
void ConvertMp3ToWav(this string mp3FilePath, string outputWavPath)
```
Converts an MP3 file to WAV format.

**ConvertMp4ToWav**
```csharp
void ConvertMp4ToWav(this string mp4FilePath, string outputWavPath)
```
Converts an MP4 file to WAV format.

**ConvertOggToWav**
```csharp
void ConvertOggToWav(this string oggFilePath, string outputWavPath)
```
Converts an OGG file to WAV format.

**ConvertToWav**
```csharp
void ConvertToWav(this string inputFilePath, string outputWavPath)
```
Auto-detects format and converts to WAV.

**ToWaveStream**
```csharp
WaveStream ToWaveStream(this string inputFilePath)
```
Opens an audio file as a WaveStream for custom processing.

**IsSupportedAudioFormat**
```csharp
bool IsSupportedAudioFormat(this string filePath)
```
Checks if the file format is supported.

### AudioEditor Class

Located in `RecordingsHelper.Core.Services`

#### Methods

**RemoveSegments**
```csharp
void RemoveSegments(string inputFile, string outputFile, List<AudioSegment> segmentsToRemove)
```
Removes specified segments from an audio file. Processes segments right-to-left to maintain timeline accuracy. Shortens the file duration.

**RemoveSegment**
```csharp
void RemoveSegment(string inputFile, string outputFile, TimeSpan start, TimeSpan end)
```
Removes a single segment from an audio file.

**RemoveSegmentsInSeconds**
```csharp
void RemoveSegmentsInSeconds(string inputFile, string outputFile, List<(double start, double end)> segments)
```
Removes segments specified as (start, end) tuples in seconds.

**MuteSegments**
```csharp
void MuteSegments(string inputFile, string outputFile, List<AudioSegment> segmentsToMute)
```
Mutes specified segments by replacing them with silence. Preserves the file duration and timeline.

**MuteSegment**
```csharp
void MuteSegment(string inputFile, string outputFile, TimeSpan start, TimeSpan end)
```
Mutes a single segment in an audio file.

**MuteSegmentsInSeconds**
```csharp
void MuteSegmentsInSeconds(string inputFile, string outputFile, List<(double start, double end)> segments)
```
Mutes segments specified as (start, end) tuples in seconds.

**GetDurationAfterRemoval**
```csharp
TimeSpan GetDurationAfterRemoval(string inputFile, List<AudioSegment> segmentsToRemove)
```
Calculates the duration of the file after segments are removed.

### AudioSegment Class

Located in `RecordingsHelper.Core.Models`

#### Properties

- `TimeSpan Start` - Start time of the segment
- `TimeSpan End` - End time of the segment
- `TimeSpan Duration` - Duration of the segment (read-only)
- `bool IsValid` - Returns true if the segment is valid (read-only)

#### Static Methods

**FromSeconds**
```csharp
static AudioSegment FromSeconds(double startSeconds, double endSeconds)
```
Creates a segment from start and end times in seconds.

**FromDuration**
```csharp
static AudioSegment FromDuration(TimeSpan start, TimeSpan duration)
```
Creates a segment from start time and duration.

## Integration Examples

### Console Application

```csharp
using RecordingsHelper.Core.Services;
using RecordingsHelper.Core.Extensions;

var stitcher = new AudioStitcher();
var files = new[] { "part1.mp3", "part2.wav" };
stitcher.StitchAudioFiles(files, "complete.wav");
```

### ASP.NET Core Web API

```csharp
using Microsoft.AspNetCore.Mvc;
using RecordingsHelper.Core.Services;

[ApiController]
[Route("api/[controller]")]
public class AudioController : ControllerBase
{
    private readonly AudioStitcher _stitcher;

    public AudioController()
    {
        _stitcher = new AudioStitcher();
    }

    [HttpPost("stitch")]
    public IActionResult StitchAudio([FromBody] StitchRequest request)
    {
        _stitcher.StitchAudioFiles(request.Files, request.OutputPath);
        return Ok(new { message = "Audio stitched successfully" });
    }
}
```

### Windows Forms / WPF

```csharp
using RecordingsHelper.Core.Services;

public class AudioProcessor
{
    private readonly AudioStitcher _stitcher = new();

    public void ProcessFiles(string[] files, string output)
    {
        _stitcher.StitchAudioFiles(files, output);
    }

    public TimeSpan GetDuration(string[] files)
    {
        return _stitcher.GetTotalDuration(files);
    }
}
```

### Dependency Injection

```csharp
// Program.cs or Startup.cs
services.AddSingleton<AudioStitcher>();

// Usage in a controller or service
public class MyService
{
    private readonly AudioStitcher _stitcher;

    public MyService(AudioStitcher stitcher)
    {
        _stitcher = stitcher;
    }

    public void Process()
    {
        _stitcher.StitchAudioFiles(files, output);
    }
}
```

## Error Handling

The library throws appropriate exceptions:
- `FileNotFoundException` - When an input file doesn't exist
- `ArgumentException` - When no input files are provided
- `NotSupportedException` - When an unsupported format is encountered
- `InvalidOperationException` - When audio format cannot be determined

Example:

```csharp
try
{
    stitcher.StitchAudioFiles(files, "output.wav");
}
catch (FileNotFoundException ex)
{
    Console.WriteLine($"File not found: {ex.Message}");
}
catch (NotSupportedException ex)
{
    Console.WriteLine($"Format not supported: {ex.Message}");
}
```

## Requirements

- .NET 9.0 or compatible
- NAudio 2.2.1 or higher
- NAudio.Vorbis 1.5.0 or higher

## Performance Considerations

- High-quality resampling (quality level 60) is used for format conversion
- Streams are properly disposed to prevent memory leaks
- Large files are processed in chunks to minimize memory usage
- Buffer size is optimized based on sample rate

## Thread Safety

The `AudioStitcher` and extension methods are not thread-safe. If you need to process multiple audio operations concurrently, create separate instances for each thread or use appropriate locking mechanisms.

## License

This library is provided as-is for audio processing needs.

## Project Structure

```
RecordingsHelper.Core/
├── Extensions/
│   └── AudioConverterExtensions.cs
├── Services/
│   └── AudioStitcher.cs
└── RecordingsHelper.Core.csproj
```

## Related Projects

- **RecordingsHelper** - Console application demonstrating library usage
