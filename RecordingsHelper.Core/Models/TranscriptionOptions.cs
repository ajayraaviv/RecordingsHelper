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
}
