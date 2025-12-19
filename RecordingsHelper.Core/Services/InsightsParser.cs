using System.Text.Json;
using RecordingsHelper.Core.Models;

namespace RecordingsHelper.Core.Services;

public class InsightsParser
{
    public static List<TranscriptInsight> LoadFromJson(string jsonFilePath)
    {
        if (!File.Exists(jsonFilePath))
            throw new FileNotFoundException("Insights JSON file not found.", jsonFilePath);

        var jsonContent = File.ReadAllText(jsonFilePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var root = JsonSerializer.Deserialize<InsightsRoot>(jsonContent, options);
        
        if (root?.Videos == null || root.Videos.Count == 0)
            return new List<TranscriptInsight>();

        // Combine transcripts from all videos
        var allTranscripts = new List<TranscriptInsight>();
        foreach (var video in root.Videos)
        {
            if (video.Insights.Transcript != null)
            {
                allTranscripts.AddRange(video.Insights.Transcript);
            }
        }

        // Sort by start time using adjusted times
        var sortedTranscripts = allTranscripts
            .OrderBy(t => t.Instances.FirstOrDefault()?.StartTime ?? TimeSpan.Zero)
            .ToList();

        // Re-assign IDs to ensure uniqueness across all videos
        for (int i = 0; i < sortedTranscripts.Count; i++)
        {
            sortedTranscripts[i].Id = i + 1;
        }

        return sortedTranscripts;
    }
}
