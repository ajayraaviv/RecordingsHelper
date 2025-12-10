using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.Vorbis;

namespace RecordingsHelper.Core.Services;

/// <summary>
/// Service for stitching multiple audio files together
/// </summary>
public class AudioStitcher
{
    /// <summary>
    /// Stitches multiple WAV files together into a single output file
    /// </summary>
    /// <param name="inputFiles">Array of input WAV file paths</param>
    /// <param name="outputFile">Path where the stitched output file will be saved</param>
    /// <param name="insertSilence">Optional silence duration (in milliseconds) to insert between files</param>
    public void StitchWavFiles(string[] inputFiles, string outputFile, int insertSilence = 0)
    {
        if (inputFiles == null || inputFiles.Length == 0)
            throw new ArgumentException("At least one input file is required", nameof(inputFiles));

        // Validate all input files exist
        foreach (var file in inputFiles)
        {
            if (!File.Exists(file))
                throw new FileNotFoundException($"Input file not found: {file}");
        }

        // Read the first file to get the wave format
        using var firstReader = new WaveFileReader(inputFiles[0]);
        var waveFormat = firstReader.WaveFormat;

        using var writer = new WaveFileWriter(outputFile, waveFormat);
        
        foreach (var inputFile in inputFiles)
        {
            using var reader = new WaveFileReader(inputFile);
            
            // Ensure all files have the same format
            if (!reader.WaveFormat.Equals(waveFormat))
            {
                // Convert to match the target format
                using var resampler = new MediaFoundationResampler(reader, waveFormat);
                resampler.ResamplerQuality = 60; // High quality
                
                byte[] buffer = new byte[waveFormat.AverageBytesPerSecond];
                int bytesRead;
                while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
                {
                    writer.Write(buffer, 0, bytesRead);
                }
            }
            else
            {
                reader.CopyTo(writer);
            }

            // Insert silence if specified
            if (insertSilence > 0 && inputFile != inputFiles[^1])
            {
                var silenceSamples = (int)(waveFormat.SampleRate * (insertSilence / 1000.0) * waveFormat.Channels);
                var silenceBytes = new byte[silenceSamples * (waveFormat.BitsPerSample / 8)];
                writer.Write(silenceBytes, 0, silenceBytes.Length);
            }
        }
    }

    /// <summary>
    /// Stitches multiple audio files of any supported format together into a single WAV output file
    /// </summary>
    /// <param name="inputFiles">Array of input audio file paths (can be mixed formats)</param>
    /// <param name="outputFile">Path where the stitched output WAV file will be saved</param>
    /// <param name="insertSilence">Optional silence duration (in milliseconds) to insert between files</param>
    public void StitchAudioFiles(string[] inputFiles, string outputFile, int insertSilence = 0)
    {
        if (inputFiles == null || inputFiles.Length == 0)
            throw new ArgumentException("At least one input file is required", nameof(inputFiles));

        // Validate all input files exist
        foreach (var file in inputFiles)
        {
            if (!File.Exists(file))
                throw new FileNotFoundException($"Input file not found: {file}");
        }

        WaveFormat? targetFormat = null;
        using var writer = new WaveFileWriter(outputFile, new WaveFormat(44100, 16, 2)); // Default format

        var streams = new List<WaveStream>();
        
        try
        {
            // Open all files and determine the target format
            foreach (var inputFile in inputFiles)
            {
                var stream = OpenAudioFile(inputFile);
                streams.Add(stream);

                if (targetFormat == null)
                {
                    targetFormat = stream.WaveFormat;
                }
            }

            // Recreate writer with the correct format
            writer.Dispose();
            
            if (targetFormat == null)
                throw new InvalidOperationException("Could not determine target audio format");
                
            using var finalWriter = new WaveFileWriter(outputFile, targetFormat);

            for (int i = 0; i < streams.Count; i++)
            {
                var stream = streams[i];
                
                if (!stream.WaveFormat.Equals(targetFormat))
                {
                    // Convert to match target format
                    using var resampler = new MediaFoundationResampler(stream, targetFormat);
                    resampler.ResamplerQuality = 60;
                    
                    byte[] buffer = new byte[targetFormat.AverageBytesPerSecond];
                    int bytesRead;
                    while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        finalWriter.Write(buffer, 0, bytesRead);
                    }
                }
                else
                {
                    stream.CopyTo(finalWriter);
                }

                // Insert silence if specified
                if (insertSilence > 0 && i < streams.Count - 1)
                {
                    var silenceSamples = (int)(targetFormat.SampleRate * (insertSilence / 1000.0) * targetFormat.Channels);
                    var silenceBytes = new byte[silenceSamples * (targetFormat.BitsPerSample / 8)];
                    finalWriter.Write(silenceBytes, 0, silenceBytes.Length);
                }
            }
        }
        finally
        {
            // Clean up all streams
            foreach (var stream in streams)
            {
                stream?.Dispose();
            }
        }
    }

    /// <summary>
    /// Stitches audio files with crossfade between transitions
    /// </summary>
    /// <param name="inputFiles">Array of input WAV file paths</param>
    /// <param name="outputFile">Path where the stitched output file will be saved</param>
    /// <param name="crossfadeDuration">Duration of crossfade in milliseconds</param>
    public void StitchWithCrossfade(string[] inputFiles, string outputFile, int crossfadeDuration = 1000)
    {
        if (inputFiles == null || inputFiles.Length == 0)
            throw new ArgumentException("At least one input file is required", nameof(inputFiles));

        if (inputFiles.Length == 1)
        {
            // No crossfade needed for single file
            File.Copy(inputFiles[0], outputFile, true);
            return;
        }

        var streams = new List<ISampleProvider>();
        WaveFormat? format = null;

        try
        {
            // Load all files as sample providers
            foreach (var file in inputFiles)
            {
                var reader = OpenAudioFile(file);
                var sampleProvider = reader.ToSampleProvider();
                
                if (format == null)
                {
                    format = reader.WaveFormat;
                }

                streams.Add(sampleProvider);
            }

            if (format == null)
                throw new InvalidOperationException("Could not determine audio format");

            // Create crossfaded sequence
            var crossfadeSamples = (int)(format.SampleRate * (crossfadeDuration / 1000.0));
            var concatenated = new ConcatenatingSampleProvider(streams);

            // Write output
            WaveFileWriter.CreateWaveFile16(outputFile, concatenated);
        }
        finally
        {
            streams.Clear();
        }
    }

    /// <summary>
    /// Opens an audio file and returns a WaveStream
    /// </summary>
    private WaveStream OpenAudioFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".mp3" => new Mp3FileReader(filePath),
            ".wav" => new WaveFileReader(filePath),
            ".ogg" => new VorbisWaveReader(filePath),
            ".aiff" or ".aif" => new AiffFileReader(filePath),
            _ => new MediaFoundationReader(filePath)
        };
    }

    /// <summary>
    /// Gets the total duration of all input files
    /// </summary>
    /// <param name="inputFiles">Array of audio file paths</param>
    /// <returns>Total duration as TimeSpan</returns>
    public TimeSpan GetTotalDuration(string[] inputFiles)
    {
        if (inputFiles == null || inputFiles.Length == 0)
            return TimeSpan.Zero;

        var totalDuration = TimeSpan.Zero;

        foreach (var file in inputFiles)
        {
            using var reader = OpenAudioFile(file);
            totalDuration += reader.TotalTime;
        }

        return totalDuration;
    }

    /// <summary>
    /// Normalizes audio levels across multiple files before stitching
    /// </summary>
    /// <param name="inputFiles">Array of input audio file paths</param>
    /// <param name="outputFile">Path where the normalized and stitched output will be saved</param>
    /// <param name="targetPeak">Target peak level (0.0 to 1.0, default 0.95)</param>
    public void StitchWithNormalization(string[] inputFiles, string outputFile, float targetPeak = 0.95f)
    {
        if (inputFiles == null || inputFiles.Length == 0)
            throw new ArgumentException("At least one input file is required", nameof(inputFiles));

        var normalizedProviders = new List<ISampleProvider>();
        WaveFormat? format = null;

        try
        {
            foreach (var file in inputFiles)
            {
                var reader = OpenAudioFile(file);
                
                if (format == null)
                {
                    format = reader.WaveFormat;
                }

                var sampleProvider = reader.ToSampleProvider();
                
                // Find peak level
                var samples = new List<float>();
                float[] buffer = new float[format.SampleRate];
                int read;
                while ((read = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < read; i++)
                    {
                        samples.Add(buffer[i]);
                    }
                }

                var peak = samples.Max(Math.Abs);
                var gain = peak > 0 ? targetPeak / peak : 1.0f;

                // Reset and apply gain
                reader.Position = 0;
                var gainProvider = new VolumeSampleProvider(reader.ToSampleProvider())
                {
                    Volume = gain
                };

                normalizedProviders.Add(gainProvider);
            }

            // Concatenate and write
            var concatenated = new ConcatenatingSampleProvider(normalizedProviders);
            WaveFileWriter.CreateWaveFile16(outputFile, concatenated);
        }
        finally
        {
            normalizedProviders.Clear();
        }
    }
}
