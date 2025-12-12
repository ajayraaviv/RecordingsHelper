# Audio Editing Features - Quick Reference

## New Capabilities Added

### 1. AudioEditor Service
New service class for removing or muting audio segments.

### 2. AudioSegment Model
Helper class for defining time ranges with validation.

### 3. Two Editing Modes

#### Remove Segments (Strip Out)
- **Physically removes** audio sections
- **Shortens** file duration
- **Changes** timeline
- Processes **right-to-left** to maintain accuracy
- Best for: cutting dead air, mistakes, unwanted content

#### Mute Segments (Replace with Silence)
- **Replaces** sections with silence
- **Preserves** file duration
- **Maintains** timeline
- Best for: censoring profanity, bleeping sensitive info, maintaining sync

## Quick Examples

### Remove Single Segment
```csharp
using RecordingsHelper.Core.Services;

var editor = new AudioEditor();
editor.RemoveSegment("audio.wav", "edited.wav", 
    TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15));
```

### Remove Multiple Segments
```csharp
using RecordingsHelper.Core.Models;

var segmentsToRemove = new List<AudioSegment>
{
    AudioSegment.FromSeconds(5, 10),
    AudioSegment.FromSeconds(30, 45),
    AudioSegment.FromSeconds(120, 135)
};
editor.RemoveSegments("podcast.wav", "cleaned.wav", segmentsToRemove);
```

### Mute Single Segment
```csharp
editor.MuteSegment("interview.wav", "censored.wav",
    TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(48));
```

### Mute Multiple Segments
```csharp
var segmentsToMute = new List<AudioSegment>
{
    AudioSegment.FromSeconds(12.5, 15.2),
    AudioSegment.FromSeconds(67.3, 71.8)
};
editor.MuteSegments("recording.wav", "muted.wav", segmentsToMute);
```

### Using Seconds (Simpler)
```csharp
// Remove
editor.RemoveSegmentsInSeconds("audio.mp3", "output.wav", 
    new List<(double, double)> { (10.5, 15.3), (45.2, 50.8) });

// Mute
editor.MuteSegmentsInSeconds("audio.mp3", "output.wav",
    new List<(double, double)> { (10.5, 15.3), (45.2, 50.8) });
```

### Check Duration After Removal
```csharp
var newDuration = editor.GetDurationAfterRemoval("audio.wav", segmentsToRemove);
Console.WriteLine($"New duration: {newDuration}");
```

## Key Design Decisions

### Why Right-to-Left Processing for Removal?
When removing segments left-to-right, each removal shifts the timeline:
- Remove 10-20s → rest shifts left by 10s
- Next segment at 30-40s is now at 20-30s
- Your original timestamps become invalid

Processing right-to-left means:
- Remove 120-135s first (later segments don't affect earlier ones)
- Remove 30-45s next (earlier segments still valid)
- Remove 5-10s last

This is handled automatically by the AudioEditor.

### Why Both Remove and Mute?
Different use cases need different approaches:

**Remove** for content you want gone:
- ✓ Reduces file size
- ✓ Creates tighter content
- ✗ Changes all timestamps
- ✗ Harder to sync with other media

**Mute** for content you want silent:
- ✓ Preserves timeline
- ✓ Easy to sync with video/transcripts
- ✓ Maintains timing references
- ✗ Doesn't reduce file size

## AudioSegment Creation Methods

```csharp
// Method 1: Constructor
var seg1 = new AudioSegment(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));

// Method 2: From seconds (easiest)
var seg2 = AudioSegment.FromSeconds(10, 20);

// Method 3: From start + duration
var seg3 = AudioSegment.FromDuration(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
```

## Validation

The editor automatically validates:
- ✓ Segments don't overlap
- ✓ Start time < End time
- ✓ All times >= 0
- ✓ End time <= file duration

## File Formats

Works with all supported formats:
- Input: MP3, WAV, OGG, MP4, M4A, AAC, WMA, AIFF, AIF
- Output: WAV (16-bit PCM)

## Documentation

- **EDITING_GUIDE.md** - Comprehensive guide with scenarios
- **RecordingsHelper.Core/README.md** - Full API documentation
- **Program.cs** - Examples 8 & 9 demonstrate editing features

## Integration with Existing Features

You can combine editing with stitching:

```csharp
var editor = new AudioEditor();
var stitcher = new AudioStitcher();

// Step 1: Clean up files
editor.RemoveSegment("intro.wav", "intro_clean.wav", ...);
editor.RemoveSegment("main.wav", "main_clean.wav", ...);

// Step 2: Stitch cleaned files
var files = new[] { "intro_clean.wav", "main_clean.wav", "outro.wav" };
stitcher.StitchAudioFiles(files, "final.wav");
```

## Performance

- Processes audio in 1-second buffers (efficient memory usage)
- WAV files are fastest (no decoding needed)
- Multiple segments processed in single pass
- Right-to-left processing ensures accuracy

## Best Practices

1. **Keep originals** - Always work on copies
2. **Validate first** - Check segment times before processing
3. **Use mute for sync** - If timing matters, use mute instead of remove
4. **Batch edits** - Process all segments in one pass
5. **Test on samples** - Try on small files first
6. **Check duration** - Use `GetDurationAfterRemoval()` to preview

## Error Handling

```csharp
try
{
    editor.RemoveSegments("input.wav", "output.wav", segments);
}
catch (FileNotFoundException ex)
{
    // Input file not found
}
catch (ArgumentException ex)
{
    // Invalid segments (overlap, out of bounds, etc.)
}
```

## What's New

- ✅ AudioEditor class added
- ✅ AudioSegment model added
- ✅ RemoveSegments / RemoveSegment methods
- ✅ MuteSegments / MuteSegment methods
- ✅ Helper methods for seconds-based input
- ✅ GetDurationAfterRemoval method
- ✅ Comprehensive editing guide
- ✅ Updated documentation
- ✅ Console app examples

Build status: ✅ All projects compile successfully
