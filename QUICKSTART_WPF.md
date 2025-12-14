# Quick Start - WPF Application

## Installation

No installation required! Just build and run:

```powershell
cd d:\Workspace\Dotnet\RecordingsHelper
dotnet run --project RecordingsHelper.WPF
```

## First Time User Guide

### Merging Audio Files

1. **Launch** the application
2. **Click** "Merge Audio Files" on the home screen
3. **Click** "Add Files" button
4. **Select** multiple audio files (Ctrl+Click for multiple selection)
5. **Reorder** files using ↑↓ arrows if needed
6. **Configure** settings:
   - **Normalize Audio**: Adjusts volume levels across all files to be consistent (recommended when files have varying volumes)
   - **Crossfade Duration**: Creates smooth transitions by overlapping files (0 = instant cut, 0.5-2 seconds recommended)
7. **Click** "Merge Files"
8. **Choose** where to save the output file
9. **Wait** for processing to complete
10. **Done!** Your merged file is ready

**Tip**: You can remove individual files by clicking the ✕ button.

### Redacting Audio

1. **Launch** the application
2. **Click** "Redact Audio Segments" on the home screen
3. **Click** "Load Audio File"
4. **Select** an audio file to edit
5. **Use the player** to find segments:
   - ▶ Play button to start playback
   - ⏸ Pause button to pause
   - ⏹ Stop button to stop and reset
   - Drag the slider to seek
6. **For each segment** you want to redact:
   - Play to the **start** of the segment
   - Click **"Set"** next to Start Time (or type it manually)
   - Play to the **end** of the segment
   - Click **"Set"** next to End Time (or type it manually)
   - Click **"Add Redaction Segment"**
7. **Choose mode**:
   - **Remove Segments**: Cuts out the audio (shortens file)
   - **Mute Segments**: Replaces with silence (keeps original length)
8. **Click** "Process File"
9. **Choose** where to save the output file
10. **Wait** for processing to complete
11. **Done!** Your redacted file is ready

**Tips**: 
- Time format is mm:ss.fff (minutes:seconds.milliseconds). Example: 01:23.500 = 1 minute, 23.5 seconds
- Click **"Clear File"** to unload the current file and start fresh with a new one

### Splitting Audio

1. **Launch** the application
2. **Click** "Split Audio File" on the home screen
3. **Click** "Load Audio File"
4. **Select** an audio file to split
5. **Use the player** to find split points:
   - ▶ Play button to start playback
   - ⏸ Pause button to pause
   - ⏹ Stop button to stop and reset
   - Drag the slider to seek
6. **For each split point**:
   - Play to the desired split position
   - Click **"Add at Current"** to add a split point at the playback position
   - **OR** manually type the time and click **"Add Split Point"**
7. **Review** your split points in the list
8. **Customize** the output file name pattern (e.g., `part_{0}.wav`)
9. **Click** "Split Audio File"
10. **Select** an output folder
11. **Wait** for processing to complete
12. **Done!** Your split files are ready in the selected folder

**Tips**: 
- You can add as many split points as needed
- Split points are automatically sorted in chronological order
- The number of output files = number of split points + 1
- Example: 2 split points = 3 output files

### Advanced Redaction (Transcript-based)

1. **Launch** the application
2. **Click** "Advanced Redaction (Transcript)" on the home screen
3. **Click** "Load Audio" to select your audio file
4. **Click** "Load Insights" to select your transcript JSON file
   - Supports Azure Video Indexer JSON format
   - The JSON file should contain transcript data with timestamps
5. **Review** the loaded transcript segments
   - Each segment shows: ID, Speaker, Time Range, Text, and Confidence
6. **Select segments** to redact using checkboxes
   - Use **"Select All"** to select all segments at once
   - Use **"Deselect All"** to clear all selections
7. **For each selected segment**, choose the action:
   - **Mute**: Replaces audio with silence (preserves timing)
   - **Remove**: Completely removes the audio segment
8. **Click** "Process Redactions"
9. **Choose** where to save the output file
10. **Wait** for processing to complete
11. **Done!** Your redacted file is ready

**Tips**: 
- Each transcript segment can have multiple time instances
- All instances of a selected segment will be processed
- Mix and match Mute/Remove actions for different segments
- Speaker ID and confidence score help identify segments
- Use "Clear All Files" to start over with new files

## Common Tasks

### Merge 3 Audio Files
```
Home → Merge Audio Files → Add Files → Select 3 files → Merge Files
```

### Remove First 10 Seconds of Audio
```
Home → Redact Audio → Load File → 
Start: 00:00.000, End: 00:10.000 → Add Segment → 
Remove Segments → Process File
```

### Mute 2 Sections
```
Home → Redact Audio → Load File →
Add Segment 1 (e.g., 00:30-00:45) → 
Add Segment 2 (e.g., 01:20-01:35) → 
Mute Segments → Process File
```

### Split Audio into 3 Parts
```
Home → Split Audio File → Load File →
Add split point at 01:30 →
Add split point at 03:00 →
Split Audio File → Select output folder
```

### Advanced Redaction (Transcript-based)
```
Home → Advanced Redaction (Transcript) → 
Load Audio File → Load Insights JSON →
Select transcript segments (multi-select with checkboxes) →
Choose Mute or Remove for each segment →
Process Redactions → Save output file
```

**About Transcript Insights**: 
- Supports Azure Video Indexer JSON format
- Each transcript segment shows speaker ID, time range, and confidence
- Multi-select enabled for bulk operations
- Individual Mute/Remove choice per segment
- Automatically processes all instances of selected transcript segments

## Keyboard Tips

- **Tab**: Navigate between controls
- **Space**: Activate focused button
- **Alt+Home**: Return to home (when not focused on text input)

## Supported File Formats

### Input (can load these):
- WAV, MP3, OGG, MP4, M4A, AAC, WMA, AIFF

### Output (saves as):
- WAV (high quality, uncompressed)

**Note**: All formats are automatically converted to WAV during processing.

## Troubleshooting

### "Error loading file"
- Check that the file exists and isn't corrupted
- Ensure the file format is supported
- Make sure the file isn't being used by another application

### "Error during merge/processing"
- Verify you have write permissions to the output folder
- Check that there's enough disk space
- Ensure all input files are accessible

### Player won't play
- Try reloading the file
- Check that the audio file is valid
- Restart the application

## Performance Tips

- **Large files** (>100MB): May take a few seconds to load and process
- **Many segments**: Processing time increases with segment count
- **Normalization**: Adds a bit of processing time but improves quality
- **Crossfade**: Requires extra buffering, slight performance impact

## Getting Help

- Check `README.md` in the RecordingsHelper.WPF folder
- Read `WPF_APPLICATION_GUIDE.md` for technical details
- Review example scenarios in the console app's `EXAMPLES.md`

## Advanced Usage

### Precise Time Entry
You can manually type times in the format `mm:ss.fff`:
- `00:00.000` - Start of file
- `01:30.250` - 1 minute, 30.25 seconds
- `10:05.000` - 10 minutes, 5 seconds

### Batch Redaction
Add multiple segments before processing:
1. Add all segments to the list
2. Review the list
3. Process once (faster than processing individually)

### Quality Settings

#### Normalize Audio
Adjusts volume levels across all files to ensure consistent loudness. 
- **When to use**: Files recorded at different volume levels (e.g., quiet intro, loud main content)
- **What it does**: Prevents jarring volume changes in the merged output
- **Example**: If File A peaks at 50% and File B peaks at 100%, normalization brings both to similar levels
- **Recommendation**: Enable when merging files from different sources

#### Crossfade Duration
Creates smooth transitions between files by overlapping the end of one file with the beginning of the next.
- **0 seconds**: Instant cut (no overlap) - abrupt transition
- **0.5-2 seconds**: Smooth, professional transitions - **recommended for most content**
- **3+ seconds**: Long, gradual blending - good for music or ambient audio
- **How it works**: The first file fades out while the second file fades in simultaneously

## Workflow Examples

### Podcast Production
1. Merge: intro.mp3 + content.wav + outro.mp3
2. Enable normalization
3. Set crossfade to 1 second
4. Export as podcast_episode.wav

### Interview Editing
1. Load interview.wav in Redact view
2. Add segments for "um", "uh", long pauses
3. Choose "Remove Segments" to shorten
4. Export as interview_edited.wav

### Audio Censoring
1. Load recording.wav in Redact view
2. Add segments for sensitive information
3. Choose "Mute Segments" to preserve timing
4. Export as recording_censored.wav

---

**Happy audio editing! 🎵**
