# WPF Application Summary

## Overview

The RecordingsHelper.WPF application provides a graphical user interface for audio file manipulation with two main features:
1. **Merge Audio Files** - Combine multiple audio files into one
2. **Redact Audio Segments** - Remove or mute specific portions of audio files

## Implementation Details

### Architecture: MVVM Pattern

The application follows the Model-View-ViewModel (MVVM) pattern using CommunityToolkit.Mvvm:

```
┌─────────────────┐
│   MainWindow    │ ──► MainViewModel
│  (Home Menu)    │        │
└─────────────────┘        ├──► MergeViewModel
                           │
                           └──► RedactViewModel
```

### Project Structure

```
RecordingsHelper.WPF/
│
├── Models/
│   ├── AudioFileItem.cs          # Represents an audio file with metadata
│   └── RedactionSegment.cs       # Represents a time segment to redact
│
├── ViewModels/
│   ├── MainViewModel.cs          # Handles navigation between views
│   ├── MergeViewModel.cs         # Manages merge functionality
│   └── RedactViewModel.cs        # Manages redaction functionality
│
├── Views/
│   ├── MainWindow.xaml(.cs)      # Main window with home menu
│   ├── MergeView.xaml(.cs)       # Merge interface
│   └── RedactView.xaml(.cs)      # Redact interface
│
├── Services/
│   └── AudioPlayerService.cs     # NAudio-based audio playback
│
├── Converters/
│   └── ValueConverters.cs        # XAML data binding converters
│
└── App.xaml(.cs)                 # Application entry point with Material Design
```

### Key Components

#### 1. MainViewModel
- **Purpose**: Central navigation controller
- **Responsibilities**:
  - Navigate between Home, Merge, and Redact views
  - Manage view instances
- **Commands**:
  - `NavigateToMergeCommand`
  - `NavigateToRedactCommand`
  - `NavigateToHomeCommand`

#### 2. MergeViewModel
- **Purpose**: Manage audio file merging
- **Features**:
  - File list management (add, remove, reorder)
  - Merge settings (normalization, crossfade)
  - Progress tracking
- **Commands**:
  - `AddFilesCommand` - Open file dialog
  - `MoveUpCommand` - Move file up in sequence
  - `MoveDownCommand` - Move file down in sequence
  - `RemoveCommand` - Remove file from list
  - `ClearCommand` - Clear all files
  - `MergeFilesCommand` - Execute merge operation

#### 3. RedactViewModel
- **Purpose**: Manage audio segment redaction
- **Features**:
  - Audio file loading and playback
  - Timeline control with millisecond precision
  - Segment selection and management
  - Remove vs. Mute mode selection
- **Commands**:
  - `LoadFileCommand` - Load audio file
  - `PlayCommand` - Play audio
  - `PauseCommand` - Pause playback
  - `StopCommand` - Stop playback
  - `SetStartTimeCommand` - Capture start time
  - `SetEndTimeCommand` - Capture end time
  - `AddSegmentCommand` - Add segment to list
  - `RemoveSegmentCommand` - Remove segment from list
  - `ClearSegmentsCommand` - Clear all segments
  - `ProcessFileCommand` - Execute redaction

#### 4. AudioPlayerService
- **Purpose**: Audio playback engine using NAudio
- **Features**:
  - Load audio files
  - Play/Pause/Stop controls
  - Position tracking (updated every 100ms)
  - Duration reporting
- **Events**:
  - `PositionChanged` - Fires when position updates
  - `PlaybackStopped` - Fires when playback ends

### UI Design

#### Material Design Integration
- **Theme**: Light theme with DeepPurple primary, Lime secondary
- **Components**: Cards, buttons, icons from MaterialDesignThemes
- **Styling**: Consistent with Material Design guidelines

#### Home Page
- Title bar with application name
- Two large cards for navigation:
  - **Merge Audio Files** card with music icon
  - **Redact Audio Segments** card with cut icon

#### Merge View Layout
```
┌─────────────────────────────────────────────────────┐
│ ← Back | Merge Audio Files                          │ Title Bar
├─────────────────────────────────┬───────────────────┤
│ Audio Files List               │ Settings          │
│ ┌──────────────────────────┐  │ ☑ Normalize Audio │
│ │ 1 🎵 file1.mp3           │  │ Crossfade: 0.5s   │
│ │    MP3 • 03:45.230  ↑↓✕  │  │ Total Files: 3    │
│ │ 2 🎵 file2.wav           │  │                   │
│ │    WAV • 02:15.120  ↑↓✕  │  │                   │
│ └──────────────────────────┘  │                   │
├─────────────────────────────────┴───────────────────┤
│ Status: Ready             [Merge Files]            │ Action Bar
└─────────────────────────────────────────────────────┘
```

#### Redact View Layout
```
┌─────────────────────────────────────────────────────┐
│ ← Back | Redact Audio Segments                      │ Title Bar
├─────────────────────────────────────────────────────┤
│ [Load Audio File]                                   │ File Selection
│ loaded_file.mp3 • Duration: 05:32.450              │
├─────────────────────────────────────────────────────┤
│ Audio Player                                        │
│ 00:00.000 ═══════●════════════════ 05:32.450       │ Timeline
│         ▶  ⏸  ⏹                                     │ Controls
│ Start: 00:00.000 [Set]  End: 00:00.000 [Set]      │ Segment Input
│                [Add Redaction Segment]              │
├─────────────────────────────────┬───────────────────┤
│ Redaction Segments             │ Options           │
│ ┌──────────────────────────┐  │ ○ Remove Segments │
│ │ ✂ 00:30.000 → 00:45.000  │  │ ● Mute Segments   │
│ │   Duration: 00:15.000  ✕ │  │ Total Segments: 2 │
│ └──────────────────────────┘  │                   │
├─────────────────────────────────┴───────────────────┤
│ Status: Ready             [Process File]           │ Action Bar
└─────────────────────────────────────────────────────┘
```

### Data Binding

#### Value Converters
1. **TimeSpanToStringConverter**
   - Converts TimeSpan ↔ "mm:ss.fff" format
   - Used for all time displays and inputs

2. **BoolToVisibilityConverter**
   - Converts bool → Visibility (Visible/Collapsed)
   - Supports "Inverse" parameter

3. **InverseBoolToVisibilityConverter**
   - Converts !bool → Visibility
   - Used for opposite visibility logic

4. **InverseBooleanConverter**
   - Converts bool ↔ !bool
   - Used for checkbox binding

### Integration with RecordingsHelper.Core

The WPF application uses the Core library for all audio processing:

```csharp
// Merging
AudioStitcher stitcher = new AudioStitcher();
stitcher.StitchWithNormalization(filePaths, outputPath, crossfadeDuration);

// Redacting - Remove
AudioEditor editor = new AudioEditor();
editor.RemoveSegments(inputPath, outputPath, segments);

// Redacting - Mute
editor.MuteSegments(inputPath, outputPath, segments);
```

### User Workflows

#### Merge Workflow
1. User clicks "Merge Audio Files" on home
2. User adds files via file dialog
3. User reorders files using up/down buttons
4. User configures normalization and crossfade
5. User clicks "Merge Files"
6. File dialog asks for output location
7. Background task performs merge
8. Success message shows output location

#### Redact Workflow
1. User clicks "Redact Audio Segments" on home
2. User loads audio file
3. Audio player displays with timeline
4. User plays audio to find segments
5. For each segment:
   - User plays to start position, clicks "Set" for start
   - User plays to end position, clicks "Set" for end
   - Or manually enters times
   - User clicks "Add Redaction Segment"
6. User selects Remove or Mute mode
7. User clicks "Process File"
8. File dialog asks for output location
9. Background task performs redaction
10. Success message shows output location

### Time Precision

All time values support millisecond precision:
- Format: `mm:ss.fff`
- TimeSpan precision: Up to microseconds (internal)
- Display precision: Milliseconds (user-friendly)
- Input format: Manual or via "Set" buttons

### Async Operations

Long-running operations run on background threads:
```csharp
await Task.Run(() =>
{
    // Audio processing operation
});
```

This prevents UI freezing during:
- Audio merging (can take several seconds for large files)
- Segment redaction (depends on number of segments)

### Progress Indication

- **IsProcessing** property controls UI state
- Buttons show loading spinner when processing
- Status messages update in real-time
- Success/error dialogs after completion

## Building and Running

### Build
```powershell
dotnet build RecordingsHelper.WPF
```

### Run
```powershell
dotnet run --project RecordingsHelper.WPF
```

### Publish (Single-File Executable)
```powershell
dotnet publish RecordingsHelper.WPF -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Dependencies

### NuGet Packages
- `MaterialDesignThemes` (5.x) - UI components
- `MaterialDesignColors` (3.x) - Color themes
- `CommunityToolkit.Mvvm` (8.x) - MVVM helpers
- `NAudio` (2.2.1) - Audio engine
- `NAudio.Vorbis` (1.5.0) - OGG support

### Project References
- `RecordingsHelper.Core` - Audio processing logic

## Future Enhancements

Possible improvements:
- Drag-and-drop file support (Windows drag to ListView)
- Visual waveform display
- Segment preview before processing
- Batch processing multiple files
- Custom output format selection (not just WAV)
- Keyboard shortcuts
- Recent files list
- Undo/Redo for segment operations
- Audio effects (fade in/out, volume adjustment)
- Real-time preview of redacted audio

## Testing

Manual testing checklist:
- [ ] Home page displays correctly
- [ ] Navigate to Merge view
- [ ] Add multiple audio files
- [ ] Reorder files up/down
- [ ] Remove individual files
- [ ] Clear all files
- [ ] Configure merge settings
- [ ] Execute merge operation
- [ ] Navigate to Redact view
- [ ] Load audio file
- [ ] Play/pause/stop audio
- [ ] Seek timeline
- [ ] Capture start/end times with Set buttons
- [ ] Manually enter times
- [ ] Add multiple segments
- [ ] Remove segments
- [ ] Toggle Remove/Mute mode
- [ ] Execute redaction operation
- [ ] Navigate back to home
- [ ] Test with different audio formats (MP3, WAV, OGG, etc.)
- [ ] Test with long audio files (>10 minutes)
- [ ] Test error handling (invalid files, invalid times)

## Troubleshooting

### Common Issues

**App doesn't start**
- Ensure .NET 9.0 runtime is installed
- Check that RecordingsHelper.Core.dll is in output directory

**Audio doesn't play**
- Verify NAudio packages are installed
- Check audio file format is supported
- Ensure audio file isn't corrupted

**UI looks wrong**
- Ensure MaterialDesignThemes packages are installed
- Check App.xaml has correct resource dictionaries

**Merge/Redact fails**
- Check output directory has write permissions
- Ensure input files exist and aren't locked
- Verify sufficient disk space

## Performance Considerations

- Large files (>100MB) may take time to process
- Multiple segments increase processing time
- Normalization adds overhead but improves quality
- Crossfade requires additional buffering
- Playback position updates every 100ms (configurable)

## Conclusion

The WPF application provides a complete, user-friendly interface for the RecordingsHelper library. It demonstrates modern WPF development with Material Design, MVVM pattern, and clean architecture. The modular design allows for easy maintenance and future enhancements.
