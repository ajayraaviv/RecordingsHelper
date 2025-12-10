using NAudio.Wave;
using NAudio.Vorbis;

namespace RecordingsHelper.Core.Extensions;

/// <summary>
/// Extension methods for converting various audio formats to WAV format
/// </summary>
public static class AudioConverterExtensions
{
    /// <summary>
    /// Converts an MP3 file to WAV format
    /// </summary>
    /// <param name="mp3FilePath">Path to the MP3 file</param>
    /// <param name="outputWavPath">Path where the WAV file will be saved</param>
    public static void ConvertMp3ToWav(this string mp3FilePath, string outputWavPath)
    {
        if (!File.Exists(mp3FilePath))
            throw new FileNotFoundException($"MP3 file not found: {mp3FilePath}");

        using var mp3Reader = new Mp3FileReader(mp3FilePath);
        WaveFileWriter.CreateWaveFile(outputWavPath, mp3Reader);
    }

    /// <summary>
    /// Converts an MP4 audio file to WAV format
    /// </summary>
    /// <param name="mp4FilePath">Path to the MP4 file</param>
    /// <param name="outputWavPath">Path where the WAV file will be saved</param>
    public static void ConvertMp4ToWav(this string mp4FilePath, string outputWavPath)
    {
        if (!File.Exists(mp4FilePath))
            throw new FileNotFoundException($"MP4 file not found: {mp4FilePath}");

        using var mp4Reader = new MediaFoundationReader(mp4FilePath);
        WaveFileWriter.CreateWaveFile(outputWavPath, mp4Reader);
    }

    /// <summary>
    /// Converts an OGG file to WAV format
    /// </summary>
    /// <param name="oggFilePath">Path to the OGG file</param>
    /// <param name="outputWavPath">Path where the WAV file will be saved</param>
    public static void ConvertOggToWav(this string oggFilePath, string outputWavPath)
    {
        if (!File.Exists(oggFilePath))
            throw new FileNotFoundException($"OGG file not found: {oggFilePath}");

        using var oggReader = new VorbisWaveReader(oggFilePath);
        WaveFileWriter.CreateWaveFile(outputWavPath, oggReader);
    }

    /// <summary>
    /// Converts any supported audio format to WAV format
    /// Supports: MP3, MP4, OGG, AIFF, WMA, and other formats supported by MediaFoundation
    /// </summary>
    /// <param name="inputFilePath">Path to the input audio file</param>
    /// <param name="outputWavPath">Path where the WAV file will be saved</param>
    public static void ConvertToWav(this string inputFilePath, string outputWavPath)
    {
        if (!File.Exists(inputFilePath))
            throw new FileNotFoundException($"Audio file not found: {inputFilePath}");

        var extension = Path.GetExtension(inputFilePath).ToLowerInvariant();

        try
        {
            IWaveProvider? reader = extension switch
            {
                ".mp3" => new Mp3FileReader(inputFilePath),
                ".wav" => new WaveFileReader(inputFilePath),
                ".ogg" => new VorbisWaveReader(inputFilePath),
                ".aiff" or ".aif" => new AiffFileReader(inputFilePath),
                _ => new MediaFoundationReader(inputFilePath) // Fallback for MP4, WMA, AAC, etc.
            };

            using (reader as IDisposable)
            {
                WaveFileWriter.CreateWaveFile(outputWavPath, reader);
            }
        }
        catch (Exception ex)
        {
            throw new NotSupportedException(
                $"Unable to convert {extension} file to WAV. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Converts an audio file to WAV format and returns the WaveStream
    /// </summary>
    /// <param name="inputFilePath">Path to the input audio file</param>
    /// <returns>A WaveStream of the converted audio</returns>
    public static WaveStream ToWaveStream(this string inputFilePath)
    {
        if (!File.Exists(inputFilePath))
            throw new FileNotFoundException($"Audio file not found: {inputFilePath}");

        var extension = Path.GetExtension(inputFilePath).ToLowerInvariant();

        return extension switch
        {
            ".mp3" => new Mp3FileReader(inputFilePath),
            ".wav" => new WaveFileReader(inputFilePath),
            ".ogg" => new VorbisWaveReader(inputFilePath),
            ".aiff" or ".aif" => new AiffFileReader(inputFilePath),
            _ => new MediaFoundationReader(inputFilePath)
        };
    }

    /// <summary>
    /// Checks if a file format is supported for conversion
    /// </summary>
    /// <param name="filePath">Path to the audio file</param>
    /// <returns>True if the format is supported, false otherwise</returns>
    public static bool IsSupportedAudioFormat(this string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var supportedFormats = new[] { ".mp3", ".wav", ".ogg", ".aiff", ".aif", ".mp4", ".m4a", ".wma", ".aac" };
        return supportedFormats.Contains(extension);
    }
}
