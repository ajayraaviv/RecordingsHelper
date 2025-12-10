# RecordingsHelper Console Application

A demonstration console application for the **RecordingsHelper.Core** class library, showcasing audio file stitching and format conversion capabilities.

## Overview

This console application provides interactive examples and documentation for using the RecordingsHelper.Core library. It demonstrates various audio processing scenarios including format conversion, file stitching, and audio normalization.

## Project Structure

```
RecordingsHelper/ (Solution Root)
├── RecordingsHelper.Core/          # Class library (reusable)
│   ├── Extensions/
│   │   └── AudioConverterExtensions.cs
│   ├── Services/
│   │   └── AudioStitcher.cs
│   └── README.md
├── RecordingsHelper/               # Console application (this project)
│   ├── Program.cs
│   ├── README.md (this file)
│   └── EXAMPLES.md
└── RecordingsHelper.sln
```

## Quick Start

### Building the Solution

```bash
# From the solution root
cd d:\Workspace\Dotnet\RecordingsHelper
dotnet build
```

### Running the Demo

```bash
# From the solution root
cd RecordingsHelper
dotnet run
```

## Using RecordingsHelper.Core in Your Own Projects

The core functionality is in the **RecordingsHelper.Core** class library, which can be referenced by any .NET project:

### Add Project Reference

```bash
dotnet add reference path/to/RecordingsHelper.Core/RecordingsHelper.Core.csproj
```

### Use in Your Code

```csharp
using RecordingsHelper.Core.Services;
using RecordingsHelper.Core.Extensions;

var stitcher = new AudioStitcher();
var files = new[] { "audio1.wav", "audio2.mp3", "audio3.ogg" };
stitcher.StitchAudioFiles(files, "output.wav");
```

## Features Demonstrated

The console application showcases:

1. ✅ **Basic WAV File Stitching** - Combine multiple WAV files
2. ✅ **Mixed Format Stitching** - Combine different audio formats (MP3, WAV, OGG, MP4)
3. ✅ **Stitching with Silence** - Add configurable silence between audio segments
4. ✅ **Audio Format Conversion** - Convert between various audio formats
5. ✅ **Normalized Stitching** - Balance audio levels across files
6. ✅ **Duration Calculation** - Calculate total playback time
7. ✅ **Working with Streams** - Advanced audio stream processing

## Example Output

When you run the application, you'll see:

```
=== Audio Stitching Helper ===

Example 1: Basic WAV File Stitching
Usage: Stitch multiple WAV files together
Code:
var stitcher = new AudioStitcher();
var wavFiles = new[] { "audio1.wav", "audio2.wav", "audio3.wav" };
stitcher.StitchWavFiles(wavFiles, "output.wav");

Example 2: Mixed Format Stitching
...
```

The application displays 7 different usage examples with code snippets.

## Supported Audio Formats

**Input Formats**:
- MP3 (.mp3)
- WAV (.wav)
- OGG Vorbis (.ogg)
- MP4/M4A (.mp4, .m4a)
- AAC (.aac)
- WMA (.wma)
- AIFF (.aiff, .aif)

**Output Format**:
- WAV (16-bit PCM)

## Code Examples from the Demo

### Basic Stitching
```csharp
using RecordingsHelper.Core.Services;

var stitcher = new AudioStitcher();
var files = new[] { "audio1.wav", "audio2.wav", "audio3.wav" };
stitcher.StitchWavFiles(files, "output.wav");
```

### Mixed Format with Silence
```csharp
var stitcher = new AudioStitcher();
var files = new[] { "intro.wav", "main.wav", "outro.wav" };
stitcher.StitchAudioFiles(files, "podcast.wav", insertSilence: 1000); // 1 second
```

### Format Conversion
```csharp
using RecordingsHelper.Core.Extensions;

"music.mp3".ConvertToWav("music.wav");
"video.mp4".ConvertToWav("audio.wav");
"sound.ogg".ConvertToWav("sound.wav");
```

### With Normalization
```csharp
var stitcher = new AudioStitcher();
var files = new[] { "quiet.wav", "loud.wav", "medium.wav" };
stitcher.StitchWithNormalization(files, "balanced.wav", targetPeak: 0.95f);
```

## Dependencies

- **RecordingsHelper.Core** (project reference) - Core audio processing library
- **.NET 9.0** - Runtime framework

The NAudio and NAudio.Vorbis packages are transitively included through the RecordingsHelper.Core reference.

## Documentation Files

- **README.md** (this file) - Console application overview
- **EXAMPLES.md** - 10 detailed real-world usage scenarios
- **../RecordingsHelper.Core/README.md** - Complete class library API documentation

## Integration Examples

See `EXAMPLES.md` for detailed scenarios including:
- Podcast production
- Music playlist creation
- Voice recording compilation
- Batch format conversion
- Lecture recording assembly
- Sound effect library creation
- Multi-language audio assembly
- Training materials creation
- Emergency broadcast systems

## Building From Source

```bash
# Clone or navigate to the solution
cd d:\Workspace\Dotnet\RecordingsHelper

# Restore dependencies
dotnet restore

# Build everything
dotnet build

# Run the console app
cd RecordingsHelper
dotnet run
```

## Project References

This console project references:
- `RecordingsHelper.Core` - The class library containing all audio processing logic

## Use Cases

This demo application is useful for:
- Learning how to use the RecordingsHelper.Core library
- Testing audio stitching and conversion functionality
- Understanding API usage patterns
- Quick reference for code examples
- Evaluating the library's capabilities

## Related Projects

- **RecordingsHelper.Core** - Reusable class library with all audio processing functionality
- This library can be integrated into web APIs, desktop applications, services, and more

## Further Information

For detailed API documentation, integration examples, and advanced usage, see:
- `RecordingsHelper.Core/README.md` - Full class library documentation
- `EXAMPLES.md` - Real-world usage scenarios
