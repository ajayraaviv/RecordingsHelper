# Audio Editing Guide

This guide covers using the `AudioEditor` service to remove or mute segments from audio files.

## Understanding the Two Approaches

### 1. Remove Segments (Strip Out)
**What it does**: Physically removes audio sections, shortening the file duration.

**When to use**:
- Cutting out dead air or long pauses
- Removing mistakes or retakes
- Eliminating unwanted content entirely
- Creating highlights or clips

**Advantages**:
- Reduces file size
- Eliminates unwanted content completely
- Creates a tighter, more concise result

**Disadvantages**:
- Changes the timeline/duration
- Makes it harder to sync with video or transcripts
- Can't easily "undo" without source file

### 2. Mute Segments (Replace with Silence)
**What it does**: Replaces audio sections with silence, preserving file duration.

**When to use**:
- Censoring profanity or sensitive information
- Bleeping out private details
- Maintaining sync with video or transcripts
- Temporary muting where timing matters

**Advantages**:
- Preserves timeline and file duration
- Easier to sync with other media
- Maintains original timing references
- Can layer beep sounds on top if needed

**Disadvantages**:
- Doesn't reduce file size
- Leaves silent gaps that might feel awkward

## Usage Examples

### Example 1: Remove Single Segment

```csharp
using RecordingsHelper.Core.Services;

var editor = new AudioEditor();

// Remove 5 seconds starting at 10 seconds
editor.RemoveSegment(
    "interview.wav",
    "edited.wav",
    TimeSpan.FromSeconds(10),
    TimeSpan.FromSeconds(15)
);
```

### Example 2: Remove Multiple Segments

```csharp
using RecordingsHelper.Core.Services;
using RecordingsHelper.Core.Models;

var editor = new AudioEditor();

// Define segments to remove (in any order)
var segmentsToRemove = new List<AudioSegment>
{
    AudioSegment.FromSeconds(5, 10),      // Remove 0:05 - 0:10
    AudioSegment.FromSeconds(30, 45),     // Remove 0:30 - 0:45
    AudioSegment.FromSeconds(120, 135),   // Remove 2:00 - 2:15
    new AudioSegment(
        TimeSpan.FromMinutes(5),          // Remove 5:00 - 5:30
        TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(30))
    )
};

// Segments are automatically processed right-to-left
editor.RemoveSegments("podcast.wav", "cleaned.wav", segmentsToRemove);

// Check the new duration
var newDuration = editor.GetDurationAfterRemoval("podcast.wav", segmentsToRemove);
Console.WriteLine($"Original: 10:00, New: {newDuration}");
```

### Example 3: Using Seconds (Simpler Syntax)

```csharp
var editor = new AudioEditor();

// Remove segments specified as (start, end) tuples in seconds
var segments = new List<(double start, double end)>
{
    (10.5, 15.3),    // Remove 10.5-15.3 seconds
    (45.7, 52.1),    // Remove 45.7-52.1 seconds
    (180.2, 195.8)   // Remove 180.2-195.8 seconds
};

editor.RemoveSegmentsInSeconds("audio.mp3", "output.wav", segments);
```

### Example 4: Mute Single Segment

```csharp
var editor = new AudioEditor();

// Replace 3 seconds with silence (censor profanity)
editor.MuteSegment(
    "interview.wav",
    "censored.wav",
    TimeSpan.FromSeconds(45),
    TimeSpan.FromSeconds(48)
);
```

### Example 5: Mute Multiple Segments

```csharp
using RecordingsHelper.Core.Services;
using RecordingsHelper.Core.Models;

var editor = new AudioEditor();

// Mute multiple segments (timeline stays intact)
var segmentsToMute = new List<AudioSegment>
{
    AudioSegment.FromSeconds(12.5, 15.2),
    AudioSegment.FromSeconds(67.3, 71.8),
    AudioSegment.FromSeconds(145.0, 148.5)
};

editor.MuteSegments("recording.wav", "muted.wav", segmentsToMute);

// Duration remains the same!
```

### Example 6: Mute Using Seconds

```csharp
var editor = new AudioEditor();

// Mute segments with simple tuple syntax
editor.MuteSegmentsInSeconds(
    "audio.mp3",
    "output.wav",
    new List<(double, double)>
    {
        (10.5, 15.3),
        (45.2, 50.8),
        (120.0, 123.5)
    }
);
```

## Real-World Scenarios

### Scenario 1: Podcast Editing - Remove Dead Air

```csharp
var editor = new AudioEditor();

// Remove long pauses and mistakes
var segments = new List<AudioSegment>
{
    AudioSegment.FromSeconds(45.2, 52.8),    // Dead air
    AudioSegment.FromSeconds(180.5, 195.2),  // Mistake/retake
    AudioSegment.FromSeconds(420.0, 435.5),  // Another pause
    AudioSegment.FromSeconds(680.3, 695.0)   // Final cut
};

editor.RemoveSegments("raw_podcast.wav", "final_podcast.wav", segments);

var saved = editor.GetDurationAfterRemoval("raw_podcast.wav", segments);
Console.WriteLine($"Saved {saved.TotalSeconds} seconds by removing dead air");
```

### Scenario 2: Interview Censoring - Mute Sensitive Info

```csharp
var editor = new AudioEditor();

// Mute personal information mentions (keeps timing for video sync)
var sensitiveSegments = new List<AudioSegment>
{
    AudioSegment.FromSeconds(67.2, 69.8),    // SSN mention
    AudioSegment.FromSeconds(145.3, 148.1),  // Address mention
    AudioSegment.FromSeconds(342.5, 345.0)   // Phone number
};

editor.MuteSegments("interview.wav", "public_version.wav", sensitiveSegments);
```

### Scenario 3: Music Track Editing

```csharp
var editor = new AudioEditor();

// Create radio edit by removing explicit lyrics
var explicitSections = new List<AudioSegment>
{
    AudioSegment.FromSeconds(45.2, 47.3),
    AudioSegment.FromSeconds(128.7, 130.1),
    AudioSegment.FromSeconds(203.4, 205.8)
};

editor.MuteSegments("explicit.wav", "radio_edit.wav", explicitSections);
```

### Scenario 4: Educational Content

```csharp
var editor = new AudioEditor();

// Remove long demonstration sections for summary version
var demonstrations = new List<AudioSegment>
{
    AudioSegment.FromSeconds(120, 180),   // Remove 1 min demo
    AudioSegment.FromSeconds(300, 420),   // Remove 2 min demo
    AudioSegment.FromSeconds(600, 720)    // Remove 2 min demo
};

editor.RemoveSegments("full_lecture.wav", "summary.wav", demonstrations);
```

## Important Considerations

### Timeline Changes (Remove)
When removing segments, remember that the timeline shifts:

**Original**: `[0-10][10-20][20-30][30-40]`  
**Remove**: 10-20 seconds  
**Result**: `[0-10][20-30][30-40]` → becomes → `[0-10][10-20][20-30]`

The segments after the removal shift earlier in time. This is why the tool processes **right-to-left** - so earlier timestamps remain valid as we work through the list.

### Overlap Validation
The editor validates that segments don't overlap:

```csharp
// ❌ This will throw an error
var segments = new List<AudioSegment>
{
    AudioSegment.FromSeconds(10, 20),
    AudioSegment.FromSeconds(15, 25)  // Overlaps with previous!
};
```

### Duration Validation
All segments must be within the file duration:

```csharp
// ❌ This will throw an error if file is only 60 seconds
var segments = new List<AudioSegment>
{
    AudioSegment.FromSeconds(55, 75)  // End time exceeds file length!
};
```

## AudioSegment Helper Methods

The `AudioSegment` class provides convenient construction methods:

```csharp
// Method 1: Constructor with TimeSpans
var segment1 = new AudioSegment(
    TimeSpan.FromSeconds(10),
    TimeSpan.FromSeconds(20)
);

// Method 2: From seconds (simplest)
var segment2 = AudioSegment.FromSeconds(10, 20);

// Method 3: From start time + duration
var segment3 = AudioSegment.FromDuration(
    TimeSpan.FromSeconds(10),
    TimeSpan.FromSeconds(10)  // 10 second duration
);

// Get segment info
Console.WriteLine(segment1.Start);      // 00:00:10
Console.WriteLine(segment1.End);        // 00:00:20
Console.WriteLine(segment1.Duration);   // 00:00:10
Console.WriteLine(segment1.IsValid);    // true
Console.WriteLine(segment1.ToString()); // "00:10.000 - 00:20.000 (00:10.000)"
```

## Performance Tips

1. **Batch Processing**: Process all edits in one pass rather than multiple files
2. **Right-to-Left**: Already handled automatically for removals
3. **File Formats**: WAV files are processed fastest; MP3/MP4 require decoding
4. **Large Files**: Segments are processed in 1-second buffers to manage memory

## Error Handling

```csharp
var editor = new AudioEditor();

try
{
    var segments = new List<AudioSegment>
    {
        AudioSegment.FromSeconds(10, 20),
        AudioSegment.FromSeconds(30, 40)
    };
    
    editor.RemoveSegments("input.wav", "output.wav", segments);
}
catch (FileNotFoundException ex)
{
    Console.WriteLine($"File not found: {ex.Message}");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Invalid segments: {ex.Message}");
}
```

## Combining with Other Features

### Edit Then Stitch

```csharp
var editor = new AudioEditor();
var stitcher = new AudioStitcher();

// Step 1: Remove unwanted segments from multiple files
editor.RemoveSegment("intro.wav", "intro_clean.wav", 
    TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
    
editor.RemoveSegment("main.wav", "main_clean.wav",
    TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(45));

// Step 2: Stitch the cleaned files together
var files = new[] { "intro_clean.wav", "main_clean.wav", "outro.wav" };
stitcher.StitchAudioFiles(files, "final_podcast.wav");
```

### Edit Then Normalize

```csharp
var editor = new AudioEditor();
var stitcher = new AudioStitcher();

// Remove segments
editor.RemoveSegments("recording.wav", "edited.wav", segments);

// Normalize the result
var files = new[] { "edited.wav" };
stitcher.StitchWithNormalization(files, "normalized.wav");
```

## Best Practices Summary

✅ **Use REMOVE when**: Content should be completely eliminated  
✅ **Use MUTE when**: Timing/sync must be preserved  
✅ **Right-to-left**: Handled automatically for removals  
✅ **Validate segments**: Check for overlaps and duration bounds  
✅ **Check duration**: Use `GetDurationAfterRemoval()` to preview changes  
✅ **Keep originals**: Always work on copies, never overwrite source files  
✅ **Test first**: Try on a small sample before processing long files
