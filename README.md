# RecordingsHelper Solution

A complete .NET solution for audio file manipulation and stitching, organized into a reusable class library and demonstration console application.

## Project Structure

```
RecordingsHelper/                           # Solution Root
├── RecordingsHelper.sln                    # Solution file
├── README.md                               # This file
│
├── RecordingsHelper.Core/                  # Class Library (Reusable)
│   ├── Extensions/
│   │   └── AudioConverterExtensions.cs    # Format conversion extensions
│   ├── Services/
│   │   └── AudioStitcher.cs               # Audio stitching service
│   ├── RecordingsHelper.Core.csproj       # Class library project
│   └── README.md                          # Library documentation
│
└── RecordingsHelper/                       # Console Application (Demo)
    ├── Program.cs                         # Demo application
    ├── RecordingsHelper.csproj            # Console app project
    ├── README.md                          # Console app documentation
    └── EXAMPLES.md                        # Usage scenarios
```

## Quick Start

### Build the Solution

```bash
cd d:\Workspace\Dotnet\RecordingsHelper
dotnet build
```

### Run the Demo Application

```bash
cd RecordingsHelper
dotnet run
```

## Projects

### RecordingsHelper.Core (Class Library)

A reusable .NET class library for audio processing:
- Stitch multiple audio files together
- Convert between audio formats (MP3, WAV, OGG, MP4, AAC, WMA, AIFF)
- Normalize audio levels
- Add silence between segments
- Calculate total duration

**Target Framework**: .NET 9.0  
**Dependencies**: NAudio 2.2.1, NAudio.Vorbis 1.5.0

See `RecordingsHelper.Core/README.md` for complete API documentation.

### RecordingsHelper (Console Application)

A demonstration console application showcasing the RecordingsHelper.Core library:
- Interactive examples
- Code snippets
- Usage documentation
- Real-world scenarios

**Target Framework**: .NET 9.0  
**Dependencies**: RecordingsHelper.Core (project reference)

See `RecordingsHelper/README.md` for console app details.

## Using RecordingsHelper.Core in Your Projects

### Add Project Reference

```bash
dotnet add reference path/to/RecordingsHelper.Core/RecordingsHelper.Core.csproj
```

### Example Usage

```csharp
using RecordingsHelper.Core.Services;
using RecordingsHelper.Core.Extensions;

// Stitch audio files
var stitcher = new AudioStitcher();
var files = new[] { "audio1.wav", "audio2.mp3", "audio3.ogg" };
stitcher.StitchAudioFiles(files, "output.wav");

// Convert formats
"music.mp3".ConvertToWav("music.wav");

// With silence between clips
stitcher.StitchAudioFiles(files, "podcast.wav", insertSilence: 1000);

// Normalize audio levels
stitcher.StitchWithNormalization(files, "balanced.wav");
```

## Features

✅ **Multiple Format Support** - MP3, WAV, OGG, MP4, M4A, AAC, WMA, AIFF  
✅ **Audio Stitching** - Seamlessly combine multiple files  
✅ **Format Conversion** - Convert between supported formats  
✅ **Silence Insertion** - Add configurable gaps between segments  
✅ **Normalization** - Balance volume levels across files  
✅ **High-Quality Resampling** - Automatic format matching  
✅ **Duration Calculation** - Get total playback time  
✅ **Stream Support** - Work with WaveStreams directly

## Supported Formats

**Input**: MP3, WAV, OGG, MP4, M4A, AAC, WMA, AIFF, AIF  
**Output**: WAV (16-bit PCM)

## Build Requirements

- .NET 9.0 SDK or compatible
- Windows, macOS, or Linux

## Dependencies

The solution uses:
- **NAudio** 2.2.1 - Core audio processing
- **NAudio.Vorbis** 1.5.0 - OGG Vorbis support

## Documentation

- **README.md** (this file) - Solution overview
- **RecordingsHelper.Core/README.md** - Class library API documentation
- **RecordingsHelper/README.md** - Console application guide
- **RecordingsHelper/EXAMPLES.md** - Real-world usage scenarios

## Integration Examples

The class library can be used in:
- **Console Applications** - Command-line tools
- **ASP.NET Core Web APIs** - Audio processing endpoints
- **Windows Forms / WPF** - Desktop applications
- **Blazor** - Web applications
- **Azure Functions** - Serverless audio processing
- **Background Services** - Audio processing workers

## Use Cases

- Podcast production
- Music playlist creation
- Voice recording compilation
- Batch format conversion
- Lecture recording assembly
- Sound effect library creation
- Multi-language audio assembly
- Training materials creation
- Emergency broadcast systems
- Automated audio workflows

## Getting Started

1. **Clone or navigate** to the solution directory
2. **Build** the solution: `dotnet build`
3. **Run** the demo: `cd RecordingsHelper && dotnet run`
4. **Explore** the examples in `RecordingsHelper/EXAMPLES.md`
5. **Reference** the library in your own projects

## Solution Commands

```bash
# Restore dependencies
dotnet restore

# Build entire solution
dotnet build

# Build specific project
dotnet build RecordingsHelper.Core/RecordingsHelper.Core.csproj

# Run console app
dotnet run --project RecordingsHelper/RecordingsHelper.csproj

# Clean build artifacts
dotnet clean
```

## Project Organization

The solution follows best practices:
- **Separation of Concerns** - Library logic separate from demo app
- **Reusability** - Core library can be used in any .NET project
- **Clear Documentation** - Comprehensive README files for each project
- **Examples** - Real-world scenarios and code snippets

## License

This project is provided as-is for audio processing needs.
