namespace RecordingsHelper.Core.Models;

public class TranscriptionSettings
{
    public string SubscriptionKey { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Language { get; set; } = "en-US";
    public string StorageAccountName { get; set; } = string.Empty;
    public string StorageAccountKey { get; set; } = string.Empty;
    public string StorageContainerName { get; set; } = "transcriptions";
}
