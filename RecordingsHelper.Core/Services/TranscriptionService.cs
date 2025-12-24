using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;
using RecordingsHelper.Core.Models;

namespace RecordingsHelper.Core.Services;

public class TranscriptionService
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(30) // Allow up to 30 minutes for transcription
    };

    // Store ongoing batch transcriptions
    private static readonly ConcurrentDictionary<string, BatchTranscriptionJob> _batchJobs = new();

    private class BatchTranscriptionJob
    {
        public Task<List<TranscriptionSegment>> Task { get; set; } = null!;
        public CancellationTokenSource CancellationTokenSource { get; set; } = new();
        public bool IsCompleted { get; set; }
        public bool IsFailed { get; set; }
        public string? ErrorMessage { get; set; }
        public List<TranscriptionSegment>? Result { get; set; }
    }

    public async Task<List<TranscriptionSegment>> TranscribeAudioAsync(
        string audioFilePath,
        TranscriptionSettings settings,
        TranscriptionOptions options,
        IProgress<TranscriptionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (options.Mode == TranscriptionMode.Batch)
        {
            return await TranscribeBatchAsync(audioFilePath, settings, options, progress, cancellationToken);
        }
        
        // Fast mode (with or without LLM enhancement)
        return await TranscribeFastAsync(audioFilePath, settings, options, progress, cancellationToken);
    }

    /// <summary>
    /// Transcribes audio using a blob URL instead of uploading the file
    /// </summary>
    public async Task<List<TranscriptionSegment>> TranscribeAudioFromBlobAsync(
        string blobUrl,
        string audioFilePath,
        TranscriptionSettings settings,
        TranscriptionOptions options,
        IProgress<TranscriptionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.SubscriptionKey))
            throw new ArgumentException("Subscription key is required.", nameof(settings));

        if (string.IsNullOrWhiteSpace(settings.Region))
            throw new ArgumentException("Region is required.", nameof(settings));

        progress?.Report(new TranscriptionProgress
        {
            ProgressPercentage = 10,
            SegmentsRecognized = 0,
            Message = "Preparing transcription request..."
        });

        // Use the transcriptions:transcribe API with blob URL
        var url = $"https://{settings.Region}.api.cognitive.microsoft.com/speechtotext/transcriptions:transcribe?api-version=2025-10-15";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Ocp-Apim-Subscription-Key", settings.SubscriptionKey);

        // Build multipart form data with definition containing audioUrl
        using var content = new MultipartFormDataContent();

        // Build definition JSON with audioUrl inside (per Microsoft docs)
        var supportsEnhancedMode = new[] { "eastus", "westus", "centralindia", "northeurope", "southeastasia" }
            .Contains(settings.Region?.ToLowerInvariant());

        var definition = new
        {
            audioUrl = blobUrl,
            locales = new[] { settings.Language },
            profanityFilterMode = options.ProfanityFilterMode,
            diarization = options.EnableDiarization ? new
            {
                enabled = true,
                maxSpeakers = options.MaxSpeakers,
                minSpeakers = 1
            } : null,
            enhancedMode = supportsEnhancedMode && options.UseLlmEnhancement ? new
            {
                enabled = true,
                task = "transcribe",
                prompt = (options.LlmPrompt ?? "").Trim()
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToArray()
            } : null
        };

        var definitionJson = JsonSerializer.Serialize(definition, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        System.Diagnostics.Debug.WriteLine($"Transcription Definition (Blob URL): {definitionJson}");

        var definitionContent = new StringContent(definitionJson, System.Text.Encoding.UTF8, "application/json");
        content.Add(definitionContent, "definition");
        request.Content = content;

        progress?.Report(new TranscriptionProgress
        {
            ProgressPercentage = 30,
            SegmentsRecognized = 0,
            Message = "Sending request to Azure Speech service..."
        });

        // Send request
        var response = await _httpClient.SendAsync(request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            System.Diagnostics.Debug.WriteLine($"Speech API Error Response: {errorContent}");
            throw new Exception($"Speech API error: {response.StatusCode} - {errorContent}");
        }

        progress?.Report(new TranscriptionProgress
        {
            ProgressPercentage = 70,
            SegmentsRecognized = 0,
            Message = "Processing transcription results..."
        });

        // Parse response (same as file upload)
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        System.Diagnostics.Debug.WriteLine($"Transcription Response: {responseJson}");
        
        var segments = ParseTranscriptionResponse(responseJson, audioFilePath);
        
        progress?.Report(new TranscriptionProgress
        {
            ProgressPercentage = 100,
            SegmentsRecognized = segments.Count,
            Message = $"Transcription completed. {segments.Count} segments found."
        });

        return segments;
    }

    private List<TranscriptionSegment> ParseTranscriptionResponse(string responseJson, string audioFilePath)
    {
        var segments = new List<TranscriptionSegment>();
        
        try
        {
            var jsonDoc = JsonDocument.Parse(responseJson);
            var root = jsonDoc.RootElement;
            
            // Try to parse phrases array first (detailed results)
            if (root.TryGetProperty("phrases", out var phrasesElement) && phrasesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var phrase in phrasesElement.EnumerateArray())
                {
                    var text = phrase.TryGetProperty("text", out var t) ? t.GetString() : string.Empty;
                    var offset = phrase.TryGetProperty("offsetMilliseconds", out var o) ? o.GetInt32() : 0;
                    var duration = phrase.TryGetProperty("durationMilliseconds", out var d) ? d.GetInt32() : 0;
                    var confidence = phrase.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0.0;
                    
                    string speaker = "Speaker 1";
                    if (phrase.TryGetProperty("speaker", out var s))
                    {
                        int speakerNum = s.GetInt32();
                        speaker = $"Speaker {speakerNum}";
                    }
                    
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        segments.Add(new TranscriptionSegment
                        {
                            StartTime = TimeSpan.FromMilliseconds(offset),
                            EndTime = TimeSpan.FromMilliseconds(offset + duration),
                            Text = text,
                            Speaker = speaker,
                            Confidence = confidence
                        });
                    }
                }
            }
            
            // If no phrases, try combinedPhrases
            if (segments.Count == 0 && root.TryGetProperty("combinedPhrases", out var combinedElement) && combinedElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var combined in combinedElement.EnumerateArray())
                {
                    var text = combined.TryGetProperty("text", out var t) ? t.GetString() : string.Empty;
                    
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        segments.Add(new TranscriptionSegment
                        {
                            StartTime = TimeSpan.Zero,
                            EndTime = GetAudioDuration(audioFilePath),
                            Text = text,
                            Speaker = "Speaker 1",
                            Confidence = 1.0
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error parsing transcription: {ex.Message}");
            throw new Exception($"Failed to parse transcription response: {ex.Message}");
        }
        
        if (segments.Count == 0)
        {
            return new List<TranscriptionSegment>();
        }

        // Merge adjacent segments from the same speaker
        var mergedSegments = MergeAdjacentSegments(segments);

        return mergedSegments;
    }

    private async Task<List<TranscriptionSegment>> TranscribeFastAsync(
        string audioFilePath,
        TranscriptionSettings settings,
        TranscriptionOptions options,
        IProgress<TranscriptionProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.SubscriptionKey))
            throw new ArgumentException("Subscription key is required.", nameof(settings));

        if (string.IsNullOrWhiteSpace(settings.Region))
            throw new ArgumentException("Region is required.", nameof(settings));

        if (!File.Exists(audioFilePath))
            throw new FileNotFoundException("Audio file not found.", audioFilePath);

        progress?.Report(new TranscriptionProgress
        {
            ProgressPercentage = 10,
            SegmentsRecognized = 0,
            Message = "Reading audio file..."
        });

        // Read audio file
        var audioBytes = await File.ReadAllBytesAsync(audioFilePath, cancellationToken);
        var fileName = Path.GetFileName(audioFilePath);

        // Use the transcriptions:transcribe API which supports detailed results with timestamps
        var url = $"https://{settings.Region}.api.cognitive.microsoft.com/speechtotext/transcriptions:transcribe?api-version=2025-10-15";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Ocp-Apim-Subscription-Key", settings.SubscriptionKey);

        // Build multipart form data
        using var content = new MultipartFormDataContent();
        
        // Add audio file
        var audioContent = new ByteArrayContent(audioBytes);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(audioContent, "audio", fileName);

        // Build definition JSON
        // Note: enhancedMode is only available in certain regions: eastus, westus, centralindia, northeurope, southeastasia
        var supportsEnhancedMode = new[] { "eastus", "westus", "centralindia", "northeurope", "southeastasia" }
            .Contains(settings.Region?.ToLowerInvariant());

        var definition = new
        {
            locales = new[] { settings.Language },
            profanityFilterMode = options.ProfanityFilterMode,
            diarization = options.EnableDiarization ? new
            {
                enabled = true,
                maxSpeakers = options.MaxSpeakers,
                minSpeakers = 1
            } : null,
            enhancedMode = supportsEnhancedMode && options.UseLlmEnhancement ? new
            {
                enabled = true,
                task = "transcribe",
                prompt = (options.LlmPrompt ?? "").Trim()
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToArray()
            } : null
        };

        var definitionJson = JsonSerializer.Serialize(definition, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        // Debug log the definition JSON
        System.Diagnostics.Debug.WriteLine($"Transcription Definition: {definitionJson}");

        var definitionContent = new StringContent(definitionJson, System.Text.Encoding.UTF8, "application/json");
        content.Add(definitionContent, "definition");
        request.Content = content;

        progress?.Report(new TranscriptionProgress
        {
            ProgressPercentage = 30,
            SegmentsRecognized = 0,
            Message = "Sending audio to Azure Speech service..."
        });

        // Send request
        var response = await _httpClient.SendAsync(request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            System.Diagnostics.Debug.WriteLine($"Speech API Error Response: {errorContent}");
            throw new Exception($"Speech API error: {response.StatusCode} - {errorContent}");
        }

        progress?.Report(new TranscriptionProgress
        {
            ProgressPercentage = 70,
            SegmentsRecognized = 0,
            Message = "Processing transcription results..."
        });

        // Parse response
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        System.Diagnostics.Debug.WriteLine($"Transcription Response: {responseJson}");
        
        var segments = ParseTranscriptionResponse(responseJson, audioFilePath);
        
        progress?.Report(new TranscriptionProgress
        {
            ProgressPercentage = 100,
            SegmentsRecognized = segments.Count,
            Message = $"Transcription completed. {segments.Count} segments found."
        });

        return segments;
    }

    private List<TranscriptionSegment> MergeAdjacentSegments(List<TranscriptionSegment> segments)
    {
        if (segments.Count == 0)
            return segments;

        var merged = new List<TranscriptionSegment>();
        var current = segments[0];

        for (int i = 1; i < segments.Count; i++)
        {
            var next = segments[i];

            // Merge if same speaker
            if (current.Speaker == next.Speaker)
            {
                // Combine text with a space
                current.Text = $"{current.Text} {next.Text}";
                // Extend end time to the end of the next segment
                current.EndTime = next.EndTime;
                // Average confidence (weighted by segment count would be better, but simple average works)
                current.Confidence = (current.Confidence + next.Confidence) / 2;
            }
            else
            {
                // Different speaker, save current and start new
                merged.Add(current);
                current = next;
            }
        }

        // Add the last segment
        merged.Add(current);

        return merged;
    }

    private async Task<List<TranscriptionSegment>> TranscribeBatchAsync(
        string audioFilePath,
        TranscriptionSettings settings,
        TranscriptionOptions options,
        IProgress<TranscriptionProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.SubscriptionKey))
            throw new ArgumentException("Subscription key is required.", nameof(settings));

        if (string.IsNullOrWhiteSpace(settings.Region))
            throw new ArgumentException("Region is required.", nameof(settings));

        if (!File.Exists(audioFilePath))
            throw new FileNotFoundException("Audio file not found.", audioFilePath);

        progress?.Report(new TranscriptionProgress
        {
            ProgressPercentage = 5,
            SegmentsRecognized = 0,
            Message = "Preparing batch transcription..."
        });

        // Read audio file
        var audioBytes = await File.ReadAllBytesAsync(audioFilePath, cancellationToken);
        var fileName = Path.GetFileName(audioFilePath);

        // Create batch transcription
        var baseUrl = $"https://{settings.Region}.api.cognitive.microsoft.com/speechtotext/v3.2";
        
        progress?.Report(new TranscriptionProgress
        {
            ProgressPercentage = 10,
            SegmentsRecognized = 0,
            Message = "Uploading audio file..."
        });

        // Step 1: Create transcription with file upload
        var transcriptionId = await CreateBatchTranscriptionAsync(
            baseUrl, 
            settings.SubscriptionKey, 
            audioBytes, 
            fileName, 
            settings.Language,
            options, 
            cancellationToken);

        progress?.Report(new TranscriptionProgress
        {
            ProgressPercentage = 20,
            SegmentsRecognized = 0,
            Message = "Audio uploaded. Waiting for transcription to complete..."
        });

        // Step 2: Poll for completion
        var transcriptionResult = await PollBatchTranscriptionAsync(
            baseUrl, 
            settings.SubscriptionKey, 
            transcriptionId, 
            progress, 
            cancellationToken);

        progress?.Report(new TranscriptionProgress
        {
            ProgressPercentage = 90,
            SegmentsRecognized = 0,
            Message = "Downloading transcription results..."
        });

        // Step 3: Get and parse results
        var segments = await GetBatchTranscriptionResultsAsync(
            baseUrl, 
            settings.SubscriptionKey, 
            transcriptionId, 
            cancellationToken);

        // Step 4: Clean up - delete the transcription
        await DeleteBatchTranscriptionAsync(baseUrl, settings.SubscriptionKey, transcriptionId, cancellationToken);

        progress?.Report(new TranscriptionProgress
        {
            ProgressPercentage = 100,
            SegmentsRecognized = segments.Count,
            Message = $"Batch transcription completed. {segments.Count} segments found."
        });

        return segments;
    }

    private async Task<string> CreateBatchTranscriptionAsync(
        string baseUrl,
        string subscriptionKey,
        byte[] audioBytes,
        string fileName,
        string language,
        TranscriptionOptions options,
        CancellationToken cancellationToken)
    {
        // For batch transcription UI, we'll use the fast API endpoint but track it as a background job
        // The v3.2 batch API requires blob storage URLs which is more complex
        // This approach uploads the file and starts transcription immediately
        
        var url = $"https://{subscriptionKey.Split('/')[0]}.api.cognitive.microsoft.com/speechtotext/transcriptions:transcribe?api-version=2024-11-15";
        
        // Use the fast transcription endpoint with file upload
        var fastUrl = url.Replace("/transcriptions", "/transcriptions:transcribe");
        
        using var request = new HttpRequestMessage(HttpMethod.Post, fastUrl);
        request.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

        // Build multipart form data for fast transcription
        using var content = new MultipartFormDataContent();

        // Add audio file
        var audioContent = new ByteArrayContent(audioBytes);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = extension switch
        {
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".mp4" => "audio/mp4",
            _ => "audio/wav"
        };
        audioContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(audioContent, "audio", fileName);

        // Build definition for fast transcription
        var definition = new
        {
            locales = new[] { language },
            profanityFilterMode = options.ProfanityFilterMode,
            diarization = options.EnableDiarization ? new
            {
                enabled = true,
                maxSpeakers = options.MaxSpeakers
            } : null
        };

        var definitionJson = JsonSerializer.Serialize(definition, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        var definitionContent = new StringContent(definitionJson, Encoding.UTF8);
        definitionContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Add(definitionContent, "definition");

        request.Content = content;

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Failed to start transcription: {response.StatusCode} - {errorContent}");
        }

        // Generate a unique ID for tracking this transcription
        var transcriptionId = Guid.NewGuid().ToString();
        
        System.Diagnostics.Debug.WriteLine($"Started background transcription: {transcriptionId} for {fileName}");
        
        // Store the response stream for later processing
        // In a real implementation, you'd save this or process it asynchronously
        // For now, we'll return the ID and handle completion via polling
        
        return transcriptionId;
    }

    private async Task<JsonElement> PollBatchTranscriptionAsync(
        string baseUrl,
        string subscriptionKey,
        string transcriptionId,
        IProgress<TranscriptionProgress>? progress,
        CancellationToken cancellationToken)
    {
        var url = $"{baseUrl}/transcriptions/{transcriptionId}";
        var maxPollingTime = TimeSpan.FromMinutes(30);
        var pollingInterval = TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < maxPollingTime)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new Exception($"Failed to get transcription status: {response.StatusCode} - {errorContent}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonDoc = JsonDocument.Parse(responseJson);
            var status = jsonDoc.RootElement.GetProperty("status").GetString();

            System.Diagnostics.Debug.WriteLine($"Batch transcription status: {status}");

            if (status == "Succeeded")
            {
                return jsonDoc.RootElement;
            }
            else if (status == "Failed")
            {
                var error = jsonDoc.RootElement.TryGetProperty("error", out var errorProp)
                    ? errorProp.GetString()
                    : "Unknown error";
                throw new Exception($"Batch transcription failed: {error}");
            }

            // Status is Running or NotStarted, wait and poll again
            var elapsed = DateTime.UtcNow - startTime;
            var progressPercent = Math.Min(20 + (int)((elapsed.TotalMinutes / maxPollingTime.TotalMinutes) * 60), 80);
            
            progress?.Report(new TranscriptionProgress
            {
                ProgressPercentage = progressPercent,
                SegmentsRecognized = 0,
                Message = $"Transcription in progress... ({elapsed.Minutes}m {elapsed.Seconds}s elapsed)"
            });

            await Task.Delay(pollingInterval, cancellationToken);
        }

        throw new TimeoutException("Batch transcription timed out after 30 minutes");
    }

    private async Task<List<TranscriptionSegment>> GetBatchTranscriptionResultsAsync(
        string baseUrl,
        string subscriptionKey,
        string transcriptionId,
        CancellationToken cancellationToken)
    {
        // Get files URL
        var filesUrl = $"{baseUrl}/transcriptions/{transcriptionId}/files";

        using var filesRequest = new HttpRequestMessage(HttpMethod.Get, filesUrl);
        filesRequest.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

        var filesResponse = await _httpClient.SendAsync(filesRequest, cancellationToken);
        
        if (!filesResponse.IsSuccessStatusCode)
        {
            var errorContent = await filesResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Failed to get transcription files: {filesResponse.StatusCode} - {errorContent}");
        }

        var filesJson = await filesResponse.Content.ReadAsStringAsync(cancellationToken);
        var filesDoc = JsonDocument.Parse(filesJson);
        
        // Find the transcription result file
        string? contentUrl = null;
        foreach (var file in filesDoc.RootElement.GetProperty("values").EnumerateArray())
        {
            var kind = file.GetProperty("kind").GetString();
            if (kind == "Transcription")
            {
                contentUrl = file.GetProperty("links").GetProperty("contentUrl").GetString();
                break;
            }
        }

        if (string.IsNullOrEmpty(contentUrl))
            throw new Exception("No transcription result file found");

        // Download transcription result
        using var resultRequest = new HttpRequestMessage(HttpMethod.Get, contentUrl);
        var resultResponse = await _httpClient.SendAsync(resultRequest, cancellationToken);
        
        if (!resultResponse.IsSuccessStatusCode)
        {
            var errorContent = await resultResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Failed to download transcription results: {resultResponse.StatusCode} - {errorContent}");
        }

        var resultJson = await resultResponse.Content.ReadAsStringAsync(cancellationToken);
        var resultDoc = JsonDocument.Parse(resultJson);

        // Parse transcription segments
        var segments = new List<TranscriptionSegment>();
        
        if (resultDoc.RootElement.TryGetProperty("recognizedPhrases", out var phrases))
        {
            foreach (var phrase in phrases.EnumerateArray())
            {
                var nBest = phrase.GetProperty("nBest")[0];
                var offsetTicks = phrase.GetProperty("offsetInTicks").GetInt64();
                var durationTicks = phrase.GetProperty("durationInTicks").GetInt64();
                
                var segment = new TranscriptionSegment
                {
                    StartTime = TimeSpan.FromTicks(offsetTicks),
                    EndTime = TimeSpan.FromTicks(offsetTicks + durationTicks),
                    Text = nBest.GetProperty("display").GetString() ?? string.Empty,
                    Confidence = nBest.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 1.0
                };

                // Get speaker if diarization was enabled
                if (phrase.TryGetProperty("speaker", out var speaker))
                {
                    segment.Speaker = $"Speaker {speaker.GetInt32()}";
                }
                else
                {
                    segment.Speaker = "Speaker 1";
                }

                segments.Add(segment);
            }
        }

        return segments;
    }

    private async Task DeleteBatchTranscriptionAsync(
        string baseUrl,
        string subscriptionKey,
        string transcriptionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{baseUrl}/transcriptions/{transcriptionId}";

            using var request = new HttpRequestMessage(HttpMethod.Delete, url);
            request.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

            await _httpClient.SendAsync(request, cancellationToken);
            System.Diagnostics.Debug.WriteLine($"Deleted batch transcription: {transcriptionId}");
        }
        catch (Exception ex)
        {
            // Don't fail if cleanup fails
            System.Diagnostics.Debug.WriteLine($"Failed to delete batch transcription: {ex.Message}");
        }
    }

    // Public methods for batch transcription management in the UI
    
    /// <summary>
    /// Converts stereo audio to mono to avoid Azure Speech diarization limitations
    /// </summary>
    private string ConvertToMonoIfNeeded(string audioFilePath)
    {
        try
        {
            using var reader = new AudioFileReader(audioFilePath);
            
            // Check if already mono
            if (reader.WaveFormat.Channels == 1)
            {
                return audioFilePath; // Already mono, no conversion needed
            }
            
            // Create temporary mono file
            var tempFile = Path.Combine(Path.GetTempPath(), $"{Path.GetFileNameWithoutExtension(audioFilePath)}_mono.wav");
            
            // Convert to mono using MediaFoundationResampler or manual conversion
            var outFormat = new WaveFormat(reader.WaveFormat.SampleRate, 1); // Mono format
            
            using (var resampler = new MediaFoundationResampler(reader, outFormat))
            {
                WaveFileWriter.CreateWaveFile(tempFile, resampler);
            }
            
            System.Diagnostics.Debug.WriteLine($"Converted {audioFilePath} to mono: {tempFile}");
            return tempFile;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to convert to mono: {ex.Message}");
            return audioFilePath; // Return original if conversion fails
        }
    }
    
    /// <summary>
    /// Uploads an audio file to Azure Blob Storage and returns the blob URL with SAS token.
    /// Automatically converts stereo files to mono to avoid Azure Speech diarization limitations.
    /// </summary>
    public async Task<string> UploadToBlobStorageAsync(
        string audioFilePath,
        string storageAccountName,
        string storageAccountKey,
        string containerName,
        string? folderPath,
        CancellationToken cancellationToken)
    {
        // Convert to mono if needed (to avoid stereo + diarization issues)
        var fileToUpload = ConvertToMonoIfNeeded(audioFilePath);
        var isTemporaryFile = fileToUpload != audioFilePath;
        
        try
        {
            var fileName = Path.GetFileName(audioFilePath); // Use original filename
            var blobName = string.IsNullOrWhiteSpace(folderPath) 
                ? fileName 
                : $"{folderPath}/{fileName}";

            var connectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={storageAccountKey};EndpointSuffix=core.windows.net";
            
            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            
            // Create container if it doesn't exist
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
            
            var blobClient = containerClient.GetBlobClient(blobName);
            
            // Upload the file
            await using var fileStream = File.OpenRead(fileToUpload);
            await blobClient.UploadAsync(fileStream, overwrite: true, cancellationToken);
            
            // Generate SAS token for individual blob (valid for 7 days)
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobName,
                Resource = "b", // b for blob
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
                ExpiresOn = DateTimeOffset.UtcNow.AddDays(7)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);
            
            var sasUri = blobClient.GenerateSasUri(sasBuilder);
            
            return sasUri.AbsoluteUri;
        }
        finally
        {
            // Clean up temporary mono file if created
            if (isTemporaryFile && File.Exists(fileToUpload))
            {
                try
                {
                    File.Delete(fileToUpload);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    /// <summary>
    /// Generates a container-level SAS URL that grants read access to all blobs in a folder.
    /// Returns format: https://account.blob.core.windows.net/container/folder?sas
    /// </summary>
    public string GenerateContainerSasUrl(
        string storageAccountName,
        string storageAccountKey,
        string containerName,
        string folderPath)
    {
        var connectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={storageAccountKey};EndpointSuffix=core.windows.net";
        
        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        
        // Generate SAS token valid for 7 days for the entire container
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            Resource = "c", // c for container
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // Allow 5 minutes clock skew
            ExpiresOn = DateTimeOffset.UtcNow.AddDays(7)
        };
        sasBuilder.SetPermissions(BlobContainerSasPermissions.Read | BlobContainerSasPermissions.List);
        
        var sasUri = containerClient.GenerateSasUri(sasBuilder);
        
        // Format: https://account.blob.core.windows.net/container/folder?sas
        // We need to insert the folder path before the query string
        var uriBuilder = new UriBuilder(sasUri);
        var basePath = uriBuilder.Path.TrimEnd('/');
        uriBuilder.Path = $"{basePath}/{folderPath.Trim('/')}";
        
        return uriBuilder.Uri.AbsoluteUri;
    }

    /// <summary>
    /// Creates a batch transcription job using blob storage URLs
    /// </summary>
    public async Task<string> CreateBatchTranscriptionWithUrlsAsync(
        List<string> blobUrls,
        TranscriptionSettings settings,
        TranscriptionOptions options,
        CancellationToken cancellationToken)
    {
        var baseUrl = $"https://{settings.Region}.api.cognitive.microsoft.com/speechtotext/v3.2";
        var url = $"{baseUrl}/transcriptions";

        // Build request payload according to Azure API v3.2 specification
        // contentUrls can be individual blob URLs or a single container/folder URL with SAS
        object payload;
        
        if (options.EnableDiarization)
        {
            payload =  new
            {
                contentUrls = blobUrls,
                locale = settings.Language,
                displayName = $"Batch-{DateTime.UtcNow:yyyyMMddHHmmss}",
                model = string.IsNullOrWhiteSpace(options.Model) ? null : new { self = options.Model },
                properties = new
                {
                    diarizationEnabled = true,
                    wordLevelTimestampsEnabled = options.ModelSupportsWordLevelTimestamps,
                    punctuationMode = "DictatedAndAutomatic",
                    profanityFilterMode = options.ProfanityFilterMode,
                    diarization = new
                    {
                        speakers = new
                        {
                            minCount = 1,
                            maxCount = options.MaxSpeakers
                        }
                    }
                }
            };
        }
        else
        {
            payload = new
            {
                contentUrls = blobUrls,
                locale = settings.Language,
                displayName = $"Batch-{DateTime.UtcNow:yyyyMMddHHmmss}",
                model = string.IsNullOrWhiteSpace(options.Model) ? null : new { self = options.Model },
                properties = new
                {
                    wordLevelTimestampsEnabled = options.ModelSupportsWordLevelTimestamps,
                    punctuationMode = "DictatedAndAutomatic",
                    profanityFilterMode = options.ProfanityFilterMode
                }
            };
        }

        var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions 
        { 
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull 
        });
        
        System.Diagnostics.Debug.WriteLine($"Batch transcription payload: {jsonPayload}");
        
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Ocp-Apim-Subscription-Key", settings.SubscriptionKey);
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to create batch transcription. Status: {response.StatusCode}, Error: {errorContent}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(responseJson);
        var transcriptionId = jsonDoc.RootElement.GetProperty("self").GetString()!;
        
        // Extract just the ID from the URL (format: .../transcriptions/{id})
        var id = transcriptionId.Split('/').Last();
        
        return id;
    }

    public async Task<string> CreateBatchTranscriptionAsync(
        string audioFilePath,
        TranscriptionSettings settings,
        TranscriptionOptions options,
        CancellationToken cancellationToken)
    {
        var audioBytes = await File.ReadAllBytesAsync(audioFilePath, cancellationToken);
        var fileName = Path.GetFileName(audioFilePath);
        var baseUrl = $"https://{settings.Region}.api.cognitive.microsoft.com/speechtotext/v3.2";

        return await CreateBatchTranscriptionAsync(
            baseUrl,
            settings.SubscriptionKey!,
            audioBytes,
            fileName,
            settings.Language!,
            options,
            cancellationToken);
    }

    public async Task<(BatchTranscriptionStatus Status, string StatusMessage, string? ErrorMessage)> GetBatchTranscriptionStatusAsync(
        string region,
        string subscriptionKey,
        string transcriptionId,
        CancellationToken cancellationToken)
    {
        var baseUrl = $"https://{region}.api.cognitive.microsoft.com/speechtotext/v3.2";
        var url = $"{baseUrl}/transcriptions/{transcriptionId}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            return (BatchTranscriptionStatus.Failed, "Error checking status", errorContent);
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        System.Diagnostics.Debug.WriteLine($"Batch status response: {responseJson}");
        
        var jsonDoc = JsonDocument.Parse(responseJson);
        var statusString = jsonDoc.RootElement.GetProperty("status").GetString();

        var status = statusString switch
        {
            "NotStarted" => BatchTranscriptionStatus.Submitted,
            "Running" => BatchTranscriptionStatus.Running,
            "Succeeded" => BatchTranscriptionStatus.Succeeded,
            "Failed" => BatchTranscriptionStatus.Failed,
            _ => BatchTranscriptionStatus.Submitted
        };

        string? errorMessage = null;
        if (status == BatchTranscriptionStatus.Failed)
        {
            // Try different error message locations
            if (jsonDoc.RootElement.TryGetProperty("error", out var errorProp))
            {
                if (errorProp.ValueKind == JsonValueKind.String)
                {
                    errorMessage = errorProp.GetString();
                }
                else if (errorProp.ValueKind == JsonValueKind.Object)
                {
                    // Error might be an object with 'message' or 'code' properties
                    if (errorProp.TryGetProperty("message", out var msgProp))
                    {
                        errorMessage = msgProp.GetString();
                    }
                    else if (errorProp.TryGetProperty("code", out var codeProp))
                    {
                        errorMessage = codeProp.GetString();
                    }
                    else
                    {
                        errorMessage = errorProp.ToString();
                    }
                }
            }
            else if (jsonDoc.RootElement.TryGetProperty("properties", out var props))
            {
                if (props.TryGetProperty("error", out var propsError))
                {
                    errorMessage = propsError.ToString();
                }
            }
            
            errorMessage ??= $"Transcription failed. Full response: {responseJson}";
            System.Diagnostics.Debug.WriteLine($"Batch transcription failed: {errorMessage}");
        }

        return (status, statusString ?? "Unknown", errorMessage);
    }

    /// <summary>
    /// Gets the transcription report with per-file status details
    /// </summary>
    public async Task<JsonDocument?> GetBatchTranscriptionReportAsync(
        string region,
        string subscriptionKey,
        string transcriptionId,
        CancellationToken cancellationToken)
    {
        var baseUrl = $"https://{region}.api.cognitive.microsoft.com/speechtotext/v3.2";
        var filesUrl = $"{baseUrl}/transcriptions/{transcriptionId}/files";

        using var filesRequest = new HttpRequestMessage(HttpMethod.Get, filesUrl);
        filesRequest.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

        var filesResponse = await _httpClient.SendAsync(filesRequest, cancellationToken);
        
        if (!filesResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var filesJson = await filesResponse.Content.ReadAsStringAsync(cancellationToken);
        var filesDoc = JsonDocument.Parse(filesJson);
        
        // Find the report file
        foreach (var file in filesDoc.RootElement.GetProperty("values").EnumerateArray())
        {
            var kind = file.GetProperty("kind").GetString();
            if (kind == "TranscriptionReport")
            {
                var reportUrl = file.GetProperty("links").GetProperty("contentUrl").GetString();
                if (string.IsNullOrEmpty(reportUrl))
                    return null;

                // Download the report
                using var reportRequest = new HttpRequestMessage(HttpMethod.Get, reportUrl);
                var reportResponse = await _httpClient.SendAsync(reportRequest, cancellationToken);
                
                if (!reportResponse.IsSuccessStatusCode)
                    return null;

                var reportJson = await reportResponse.Content.ReadAsStringAsync(cancellationToken);
                System.Diagnostics.Debug.WriteLine($"Transcription report: {reportJson}");
                
                return JsonDocument.Parse(reportJson);
            }
        }

        return null;
    }

    public async Task<List<TranscriptionSegment>> DownloadBatchTranscriptionResultsAsync(
        string region,
        string subscriptionKey,
        string transcriptionId,
        CancellationToken cancellationToken)
    {
        var baseUrl = $"https://{region}.api.cognitive.microsoft.com/speechtotext/v3.2";
        return await GetBatchTranscriptionResultsAsync(baseUrl, subscriptionKey, transcriptionId, cancellationToken);
    }

    /// <summary>
    /// Downloads results for a specific file from a batch transcription job.
    /// For single-file batches, this returns the same results as DownloadBatchTranscriptionResultsAsync.
    /// For multi-file batches, extracts results for the specified source blob URL.
    /// </summary>
    public async Task<List<TranscriptionSegment>> DownloadBatchFileResultsAsync(
        string region,
        string subscriptionKey,
        string transcriptionId,
        string sourceBlobUrl,
        CancellationToken cancellationToken)
    {
        var baseUrl = $"https://{region}.api.cognitive.microsoft.com/speechtotext/v3.2";
        
        // Get files URL
        var filesUrl = $"{baseUrl}/transcriptions/{transcriptionId}/files";

        using var filesRequest = new HttpRequestMessage(HttpMethod.Get, filesUrl);
        filesRequest.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

        var filesResponse = await _httpClient.SendAsync(filesRequest, cancellationToken);
        
        if (!filesResponse.IsSuccessStatusCode)
        {
            var errorContent = await filesResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Failed to get transcription files: {filesResponse.StatusCode} - {errorContent}");
        }

        var filesJson = await filesResponse.Content.ReadAsStringAsync(cancellationToken);
        var filesDoc = JsonDocument.Parse(filesJson);
        
        // Find all transcription result files
        var transcriptionFiles = new List<JsonElement>();
        foreach (var file in filesDoc.RootElement.GetProperty("values").EnumerateArray())
        {
            var kind = file.GetProperty("kind").GetString();
            if (kind == "Transcription")
            {
                transcriptionFiles.Add(file);
            }
        }

        if (!transcriptionFiles.Any())
            throw new Exception("No transcription result files found");

        // Need to download each result file and check its "source" property to match with our blob URL
        string? matchingContentUrl = null;
        
        if (transcriptionFiles.Count == 1)
        {
            // Only one file, use it
            matchingContentUrl = transcriptionFiles[0].GetProperty("links").GetProperty("contentUrl").GetString();
        }
        else
        {
            // Multiple files - need to check each one's source property
            var fileName = Path.GetFileName(new Uri(sourceBlobUrl).LocalPath);
            
            foreach (var file in transcriptionFiles)
            {
                var contentUrl = file.GetProperty("links").GetProperty("contentUrl").GetString();
                
                // Download the JSON to check the source
                using var checkRequest = new HttpRequestMessage(HttpMethod.Get, contentUrl);
                var checkResponse = await _httpClient.SendAsync(checkRequest, cancellationToken);
                
                if (checkResponse.IsSuccessStatusCode)
                {
                    var jsonContent = await checkResponse.Content.ReadAsStringAsync(cancellationToken);
                    var jsonDoc = JsonDocument.Parse(jsonContent);
                    
                    // Check if this result has a source that matches our blob URL
                    if (jsonDoc.RootElement.TryGetProperty("source", out var source))
                    {
                        var sourceUrl = source.GetString();
                        if (sourceUrl != null && sourceUrl.Contains(fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            matchingContentUrl = contentUrl;
                            break;
                        }
                    }
                }
            }
            
            // Fallback: if no match found, use the first one
            matchingContentUrl ??= transcriptionFiles[0].GetProperty("links").GetProperty("contentUrl").GetString();
        }

        if (string.IsNullOrEmpty(matchingContentUrl))
            throw new Exception("No transcription result file found");

        // Download transcription result
        using var resultRequest = new HttpRequestMessage(HttpMethod.Get, matchingContentUrl);
        var resultResponse = await _httpClient.SendAsync(resultRequest, cancellationToken);
        
        if (!resultResponse.IsSuccessStatusCode)
        {
            var errorContent = await resultResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Failed to download transcription results: {resultResponse.StatusCode} - {errorContent}");
        }

        var resultJson = await resultResponse.Content.ReadAsStringAsync(cancellationToken);
        
        // Log the JSON for debugging
        System.Diagnostics.Debug.WriteLine($"Transcription result JSON for {sourceBlobUrl}: {resultJson.Substring(0, Math.Min(500, resultJson.Length))}");
        
        var resultDoc = JsonDocument.Parse(resultJson);

        // Parse transcription segments - handle different possible formats
        var segments = new List<TranscriptionSegment>();
        
        // Try format 1: recognizedPhrases (batch API v3.2 format)
        if (resultDoc.RootElement.TryGetProperty("recognizedPhrases", out var recognizedPhrases))
        {
            foreach (var phrase in recognizedPhrases.EnumerateArray())
            {
                try
                {
                    // Get the best transcription
                    if (!phrase.TryGetProperty("nBest", out var nBestArray) || nBestArray.GetArrayLength() == 0)
                        continue;
                    
                    var nBest = nBestArray[0];
                    
                    // Get timing information - handle both int64 and double formats
                    long offsetTicks = 0;
                    long durationTicks = 0;
                    
                    if (phrase.TryGetProperty("offsetInTicks", out var offsetProp))
                    {
                        offsetTicks = offsetProp.ValueKind == JsonValueKind.Number
                            ? (long)offsetProp.GetDouble()
                            : 0;
                    }
                    
                    if (phrase.TryGetProperty("durationInTicks", out var durationProp))
                    {
                        durationTicks = durationProp.ValueKind == JsonValueKind.Number
                            ? (long)durationProp.GetDouble()
                            : 0;
                    }
                    
                    var segment = new TranscriptionSegment
                    {
                        StartTime = TimeSpan.FromTicks(offsetTicks),
                        EndTime = TimeSpan.FromTicks(offsetTicks + durationTicks),
                        Text = nBest.GetProperty("display").GetString() ?? string.Empty,
                        Confidence = nBest.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 1.0
                    };

                    // Get speaker information (may be at phrase level or nBest level)
                    if (phrase.TryGetProperty("speaker", out var phraseLevel))
                    {
                        segment.Speaker = $"Speaker {phraseLevel.GetInt32()}";
                    }
                    else if (nBest.TryGetProperty("speaker", out var nBestSpeaker))
                    {
                        segment.Speaker = $"Speaker {nBestSpeaker.GetInt32()}";
                    }
                    else
                    {
                        segment.Speaker = "Speaker 1";
                    }

                    segments.Add(segment);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing phrase: {ex.Message}");
                    // Continue with next phrase
                }
            }
        }
        // Try format 2: combinedRecognizedPhrases (alternative format)
        else if (resultDoc.RootElement.TryGetProperty("combinedRecognizedPhrases", out var combinedPhrases))
        {
            // Get duration if available
            TimeSpan totalDuration = TimeSpan.Zero;
            if (resultDoc.RootElement.TryGetProperty("durationInTicks", out var durationTicks))
            {
                totalDuration = TimeSpan.FromTicks(durationTicks.GetInt64());
            }
            else if (resultDoc.RootElement.TryGetProperty("durationMilliseconds", out var durationMs))
            {
                totalDuration = TimeSpan.FromMilliseconds(durationMs.GetDouble());
            }

            // This format has combined text, create segments per channel
            foreach (var combined in combinedPhrases.EnumerateArray())
            {
                // Get text from either 'display' or 'lexical' property
                string text = string.Empty;
                if (combined.TryGetProperty("display", out var displayProp))
                {
                    text = displayProp.GetString() ?? string.Empty;
                }
                else if (combined.TryGetProperty("lexical", out var lexicalProp))
                {
                    text = lexicalProp.GetString() ?? string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    var segment = new TranscriptionSegment
                    {
                        StartTime = TimeSpan.Zero,
                        EndTime = totalDuration,
                        Text = text,
                        Confidence = 1.0,
                        Speaker = "Speaker 1"
                    };

                    // Check for channel info
                    if (combined.TryGetProperty("channel", out var channelProp))
                    {
                        segment.Speaker = $"Channel {channelProp.GetInt32()}";
                    }

                    segments.Add(segment);
                }
            }
        }
        else
        {
            throw new Exception($"Unrecognized transcription result format. JSON structure: {resultJson.Substring(0, Math.Min(500, resultJson.Length))}");
        }

        if (segments.Count == 0)
        {
            throw new Exception("No transcription segments found in the result");
        }

        // Merge adjacent segments from the same speaker (same as fast transcription)
        var mergedSegments = MergeAdjacentSegments(segments);

        return mergedSegments;
    }

    private TimeSpan GetAudioDuration(string audioFilePath)
    {
        try
        {
            using var reader = new NAudio.Wave.AudioFileReader(audioFilePath);
            return reader.TotalTime;
        }
        catch
        {
            // Default to 1 hour if we can't read the file
            return TimeSpan.FromHours(1);
        }
    }

    public async Task<string> ExportToJsonAsync(List<TranscriptionSegment> segments, string outputPath)
    {
        var exportData = segments.Select(s => new
        {
            start = s.StartTime.ToString(@"hh\:mm\:ss"),
            end = s.EndTime.ToString(@"hh\:mm\:ss"),
            speaker = s.Speaker,
            text = s.Text
        });

        var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        await File.WriteAllTextAsync(outputPath, json);
        return outputPath;
    }

    public async Task<string> ExportToTextAsync(List<TranscriptionSegment> segments, string outputPath)
    {
        var lines = segments.Select(s => 
            $"{s.Speaker} [{s.StartTime:hh\\:mm\\:ss} - {s.EndTime:hh\\:mm\\:ss}]\n{s.Text}\n");
        
        await File.WriteAllLinesAsync(outputPath, lines);
        return outputPath;
    }

    /// <summary>
    /// Gets available base models for batch transcription in the specified region
    /// Filters for en-US locale and returns most recent 15 models including Whisper
    /// </summary>
    public async Task<List<SpeechModel>> GetAvailableModelsAsync(
        string region,
        string subscriptionKey,
        CancellationToken cancellationToken)
    {
        var baseUrl = $"https://{region}.api.cognitive.microsoft.com/speechtotext/v3.2";
        // Filter by en-US locale
        var url = $"{baseUrl}/models/base?filter=locale eq 'en-US'";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to get models. Status: {response.StatusCode}, Error: {errorContent}");
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        System.Diagnostics.Debug.WriteLine($"Models API Response: {responseJson}");
        var jsonDoc = JsonDocument.Parse(responseJson);

        var models = new List<SpeechModel>();
        
        if (jsonDoc.RootElement.TryGetProperty("values", out var values))
        {
            foreach (var model in values.EnumerateArray())
            {
                if (model.TryGetProperty("self", out var selfLink))
                {
                    var modelUrl = selfLink.GetString();
                    var displayName = model.TryGetProperty("displayName", out var nameProperty) 
                        ? nameProperty.GetString() 
                        : modelUrl;
                    
                    // Check if model supports batch transcription via properties.features.supportsTranscriptions
                    bool supportsBatchTranscription = false;
                    bool supportsWordLevelTimestamps = false; // Default to false
                    if (model.TryGetProperty("properties", out var properties))
                    {
                        if (properties.TryGetProperty("features", out var features))
                        {
                            if (features.TryGetProperty("supportsTranscriptions", out var supportsTranscriptions))
                            {
                                supportsBatchTranscription = supportsTranscriptions.GetBoolean();
                            }
                            
                            // Check for word-level timestamp support - if "Lexical" is in supportedOutputFormats
                            if (features.TryGetProperty("supportedOutputFormats", out var outputFormats))
                            {
                                foreach (var format in outputFormats.EnumerateArray())
                                {
                                    var formatStr = format.GetString();
                                    if (formatStr != null && formatStr.Equals("Lexical", StringComparison.OrdinalIgnoreCase))
                                    {
                                        supportsWordLevelTimestamps = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    
                    // Skip models that don't support batch transcription
                    if (!supportsBatchTranscription)
                    {
                        continue;
                    }
                    
                    // Extract model ID from self link (last segment)
                    var modelId = modelUrl?.Split('/').LastOrDefault();
                    
                    // Try to get creation date for sorting
                    DateTime? createdDateTime = null;
                    if (model.TryGetProperty("createdDateTime", out var createdProp))
                    {
                        if (DateTime.TryParse(createdProp.GetString(), out var parsed))
                        {
                            createdDateTime = parsed;
                        }
                    }
                    
                    if (!string.IsNullOrWhiteSpace(modelUrl) && !string.IsNullOrWhiteSpace(displayName))
                    {
                        models.Add(new SpeechModel 
                        { 
                            DisplayName = displayName, 
                            SelfLink = modelUrl,
                            ModelId = modelId ?? "",
                            CreatedDateTime = createdDateTime,
                            SupportsWordLevelTimestamps = supportsWordLevelTimestamps
                        });
                    }
                }
            }
        }

        // Sort by creation date descending (most recent first) and take top 15
        return models
            .OrderByDescending(m => m.CreatedDateTime ?? DateTime.MinValue)
            .Take(15)
            .ToList();
    }
}

// LLM Transcription API Response Models
internal class LlmTranscriptionResponse
{
    [JsonPropertyName("durationMilliseconds")]
    public int DurationMilliseconds { get; set; }

    [JsonPropertyName("combinedPhrases")]
    public List<CombinedPhrase>? CombinedPhrases { get; set; }

    [JsonPropertyName("phrases")]
    public List<Phrase>? Phrases { get; set; }
}

internal class CombinedPhrase
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

internal class Phrase
{
    [JsonPropertyName("offsetMilliseconds")]
    public int OffsetMilliseconds { get; set; }

    [JsonPropertyName("durationMilliseconds")]
    public int DurationMilliseconds { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("words")]
    public List<Word>? Words { get; set; }

    [JsonPropertyName("locale")]
    public string? Locale { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("speaker")]
    public string? Speaker { get; set; }
}

internal class Word
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("offsetMilliseconds")]
    public int OffsetMilliseconds { get; set; }

    [JsonPropertyName("durationMilliseconds")]
    public int DurationMilliseconds { get; set; }
}
