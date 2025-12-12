# RecordingsHelper Solution

A complete .NET solution for audio file manipulation, featuring a reusable class library, console demo application, and modern WPF desktop application.

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
│   │   ├── AudioStitcher.cs               # Audio stitching service
│   │   └── AudioEditor.cs                 # Audio editing service
│   ├── Models/
│   │   └── AudioSegment.cs                # Time segment model
│   ├── RecordingsHelper.Core.csproj       # Class library project
│   └── README.md                          # Library documentation
│
├── RecordingsHelper/                       # Console Application (Demo)
│   ├── Program.cs                         # Demo application
│   ├── RecordingsHelper.csproj            # Console app project
│   ├── README.md                          # Console app documentation
│   └── EXAMPLES.md                        # Usage scenarios
│
└── RecordingsHelper.WPF/                   # WPF Desktop Application
    ├── Models/                            # Data models
    ├── ViewModels/                        # MVVM ViewModels
    ├── Views/                             # XAML views
    ├── Services/                          # Application services
    ├── Converters/                        # Value converters
    ├── RecordingsHelper.WPF.csproj        # WPF project
    └── README.md                          # WPF documentation
```

## Quick Start

### Build the Solution

```bash
cd d:\Workspace\Dotnet\RecordingsHelper
dotnet build
```

### Run the Console Demo

```bash
cd RecordingsHelper
dotnet run
```

### Run the WPF Application

```bash
dotnet run --project RecordingsHelper.WPF
```

## Projects

### RecordingsHelper.Core (Class Library)

A reusable .NET class library for audio processing:
- Stitch multiple audio files together
- Convert between audio formats (MP3, WAV, OGG, MP4, AAC, WMA, AIFF)
- Remove or mute audio segments with millisecond precision
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

### RecordingsHelper.WPF (Desktop Application)

A modern WPF desktop application with Material Design UI:
- **Merge Audio Files** - Intuitive drag-drop interface for combining audio files
- **Redact Audio** - Visual timeline editor with millisecond-precision segment selection
- **Audio Player** - Built-in playback with play/pause/stop controls
- **Remove vs Mute** - Choose to completely remove segments or replace with silence

**Target Framework**: .NET 9.0-windows  
**Dependencies**: RecordingsHelper.Core, MaterialDesignThemes, CommunityToolkit.Mvvm, NAudio

See `RecordingsHelper.WPF/README.md` and `WPF_APPLICATION_GUIDE.md` for detailed documentation.

## Using RecordingsHelper.Core in Your Projects

### Add Project Reference

```bash
dotnet add reference path/to/RecordingsHelper.Core/RecordingsHelper.Core.csproj
```

### Example Usage

```csharp
using RecordingsHelper.Core.Services;
using RecordingsHelper.Core.Extensions;
using RecordingsHelper.Core.Models;

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

// Remove unwanted segments
var editor = new AudioEditor();
var segmentsToRemove = new List<AudioSegment>
{
    AudioSegment.FromSeconds(10, 15),
    AudioSegment.FromSeconds(45, 52)
};
editor.RemoveSegments("recording.wav", "edited.wav", segmentsToRemove);

// Mute sensitive sections (preserves timeline)
editor.MuteSegment("interview.wav", "censored.wav", 
    TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(33));
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
✅ **Remove Segments** - Strip out unwanted sections (changes timeline)  
✅ **Mute Segments** - Replace sections with silence (preserves timeline)

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
- **WPF_APPLICATION_GUIDE.md** - Complete WPF application documentation
- **RecordingsHelper.Core/README.md** - Class library API documentation
- **RecordingsHelper/README.md** - Console application guide
- **RecordingsHelper/EXAMPLES.md** - Real-world usage scenarios
- **RecordingsHelper/EDITING_GUIDE.md** - Comprehensive audio editing guide
- **RecordingsHelper.WPF/README.md** - WPF application user guide

## Integration Examples

The class library can be used in:
- **WPF Desktop Applications** ✨ - Rich graphical interfaces (included in solution)
- **Console Applications** - Command-line tools (included in solution)
- **ASP.NET Core Web APIs** - Audio processing endpoints
- **Windows Forms** - Legacy desktop applications
- **Blazor** - Web applications
- **Azure Functions** - Serverless audio processing
- **Background Services** - Audio processing workers

## Use Cases

- **Podcast Production** - Combine intro, content, and outro segments
- **Audio Redaction** - Remove or mute sensitive information
- **Music Playlist Creation** - Merge multiple tracks
- **Voice Recording Compilation** - Assemble interview segments
- **Batch Format Conversion** - Convert multiple files at once
- **Lecture Recording Assembly** - Combine class sessions
- **Sound Effect Library Creation** - Build custom audio libraries
- **Multi-language Audio Assembly** - Combine translations
- **Training Materials Creation** - Build educational content
- **Emergency Broadcast Systems** - Automated audio workflows

## Getting Started

### For End Users (WPF Application)
1. **Build** the solution: `dotnet build`
2. **Run** the WPF app: `dotnet run --project RecordingsHelper.WPF`
3. **Explore** the Merge and Redact features through the UI

### For Developers (Library)
1. **Clone or navigate** to the solution directory
2. **Build** the solution: `dotnet build`
3. **Run** the console demo: `cd RecordingsHelper && dotnet run`
4. **Explore** the examples in `RecordingsHelper/EXAMPLES.md`
5. **Reference** the library in your own projects

### For Contributors
1. **Read** `WPF_APPLICATION_GUIDE.md` for architecture details
2. **Review** the MVVM pattern implementation
3. **Test** changes with both console and WPF apps

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
