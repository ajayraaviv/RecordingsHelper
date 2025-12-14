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

        return root.Videos[0].Insights.Transcript ?? new List<TranscriptInsight>();
    }
}
