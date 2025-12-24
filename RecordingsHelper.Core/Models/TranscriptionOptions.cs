namespace RecordingsHelper.Core.Models;

public enum TranscriptionMode
{
    Fast,      // Fast/Real-time transcription
    Batch      // Batch transcription for large files
}

public class TranscriptionOptions
{
    public TranscriptionMode Mode { get; set; } = TranscriptionMode.Fast;
    public bool UseLlmEnhancement { get; set; } = false;
    public bool EnableDiarization { get; set; } = true;
    public int MaxSpeakers { get; set; } = 10;
    
    // LLM-specific options (only used when UseLlmEnhancement = true)
    public string? LlmPrompt { get; set; }
    
    // Profanity filter mode: "None", "Masked", "Removed", "Tags"
    public string ProfanityFilterMode { get; set; } = "Masked";
    
    // Model self link (optional, if not specified uses default)
    public string? Model { get; set; }
    
    // Whether the selected model supports word-level timestamps
    public bool ModelSupportsWordLevelTimestamps { get; set; } = true;
}

public class SpeechModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string SelfLink { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public DateTime? CreatedDateTime { get; set; }
    public bool SupportsWordLevelTimestamps { get; set; } = true;
}
