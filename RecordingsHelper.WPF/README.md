# RecordingsHelper.WPF

A modern WPF desktop application for audio file manipulation with an intuitive user interface.

## Features

### 🎵 Merge Audio Files
- Add multiple audio files in any supported format
- Drag and reorder files using up/down buttons
- Configure merge settings:
  - Audio normalization for consistent volume
  - Crossfade duration for smooth transitions
- Real-time preview of file count and order
- Export merged audio as WAV

### ✂️ Redact Audio Segments
- Load audio files with built-in player
- Precise timeline control with millisecond accuracy (mm:ss.fff)
- Interactive playback controls (Play, Pause, Stop)
- Select segments using:
  - Manual time input
  - "Set" buttons to capture current playback position
- Choose redaction mode:
  - **Remove Segments**: Completely remove audio sections (shortens duration)
  - **Mute Segments**: Replace sections with silence (preserves duration and timing)
- Visual segment list showing start/end times and durations
- Export processed audio as WAV

## User Interface

The application features a modern Material Design interface with:
- **Home Page**: Menu cards for quick access to Merge and Redact functions
- **Merge View**: File list with reordering, settings panel, and action bar
- **Redact View**: Audio player, segment selection, and processing options

## Technical Stack

- **.NET 9.0**: Latest framework
- **WPF**: Windows Presentation Foundation
- **Material Design**: Modern UI components
- **MVVM Pattern**: Clean separation of concerns using CommunityToolkit.Mvvm
- **NAudio**: Audio processing engine
- **RecordingsHelper.Core**: Shared audio processing library

## Supported Audio Formats

- WAV (Wave)
- MP3 (MPEG Audio)
- OGG (Ogg Vorbis)
- MP4/M4A (MPEG-4 Audio)
- AAC (Advanced Audio Coding)
- WMA (Windows Media Audio)
- AIFF (Audio Interchange File Format)

All formats are automatically converted to WAV for processing.

## Getting Started

### Prerequisites

- Windows 10/11
- .NET 9.0 SDK
- Visual Studio 2022 (recommended) or VS Code

### Running the Application

```powershell
# From the solution root
dotnet run --project RecordingsHelper.WPF

# Or build and run the executable
dotnet build RecordingsHelper.WPF
cd RecordingsHelper.WPF\bin\Debug\net9.0-windows
.\RecordingsHelper.WPF.exe
```

## Usage Guide

### Merging Audio Files

1. Click **"Merge Audio Files"** on the home page
2. Click **"Add Files"** to select audio files
3. Use up/down arrows to reorder files
4. Configure normalization and crossfade settings
5. Click **"Merge Files"** and choose output location

### Redacting Audio

1. Click **"Redact Audio Segments"** on the home page
2. Click **"Load Audio File"** to select a file
3. Use the player to preview your audio
4. Define segments to redact:
   - Play to the start position and click **"Set"** for Start Time
   - Play to the end position and click **"Set"** for End Time
   - Or manually enter times in mm:ss.fff format
   - Click **"Add Redaction Segment"**
5. Choose **"Remove Segments"** or **"Mute Segments"**
6. Click **"Process File"** and choose output location

## Time Format

All times are displayed with millisecond precision:
- Format: `mm:ss.fff`
- Example: `02:35.125` = 2 minutes, 35 seconds, 125 milliseconds

## Architecture

```
RecordingsHelper.WPF/
├── Models/              # Data models (AudioFileItem, RedactionSegment)
├── ViewModels/          # MVVM ViewModels (MainViewModel, MergeViewModel, RedactViewModel)
├── Views/               # XAML views (MainWindow, MergeView, RedactView)
├── Services/            # Services (AudioPlayerService)
├── Converters/          # Value converters for data binding
└── App.xaml            # Application resources and Material Design themes
```

## Dependencies

- **MaterialDesignThemes**: UI components and styling
- **MaterialDesignColors**: Color palettes
- **CommunityToolkit.Mvvm**: MVVM helpers (ObservableObject, RelayCommand)
- **NAudio**: Audio file reading and playback
- **NAudio.Vorbis**: OGG format support
- **RecordingsHelper.Core**: Core audio processing logic

## Project Reference

This WPF application uses the `RecordingsHelper.Core` class library for all audio processing operations:
- `AudioStitcher`: Merging functionality
- `AudioEditor`: Redaction (remove/mute) functionality
- `AudioConverterExtensions`: Format conversion

## License

This project is part of the RecordingsHelper solution.
