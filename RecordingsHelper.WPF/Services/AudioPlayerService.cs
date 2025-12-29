using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using NAudio.Wave;

namespace RecordingsHelper.WPF.Services;

public class AudioPlayerService : IDisposable
{
    private WaveOutEvent? _waveOut;
    private AudioFileReader? _audioFileReader;
    private string? _currentFilePath;
    private string? _tempFilePath;
    private static readonly HttpClient _httpClient = new();

    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler? PlaybackStopped;

    public bool IsPlaying => _waveOut?.PlaybackState == PlaybackState.Playing;
    public bool IsPaused => _waveOut?.PlaybackState == PlaybackState.Paused;
    public TimeSpan Duration => _audioFileReader?.TotalTime ?? TimeSpan.Zero;
    public TimeSpan Position
    {
        get => _audioFileReader?.CurrentTime ?? TimeSpan.Zero;
        set
        {
            if (_audioFileReader != null)
            {
                _audioFileReader.CurrentTime = value;
                PositionChanged?.Invoke(this, value);
            }
        }
    }

    public void LoadFile(string filePath)
    {
        Stop();
        _currentFilePath = filePath;

        _audioFileReader = new AudioFileReader(filePath);
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_audioFileReader);
        _waveOut.PlaybackStopped += OnPlaybackStopped;
    }

    public async Task LoadFileFromUrlAsync(string url)
    {
        Stop();
        
        // Download file to temp location
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"audio_{Guid.NewGuid()}.tmp");
        
        using var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        
        // Use a scope to ensure the file stream is closed before we load it
        {
            await using var fileStream = new FileStream(_tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream);
        } // fileStream is disposed here
        
        // Load the downloaded file - file is now closed and can be read
        _currentFilePath = _tempFilePath;
        _audioFileReader = new AudioFileReader(_tempFilePath);
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_audioFileReader);
        _waveOut.PlaybackStopped += OnPlaybackStopped;
    }

    public void Play()
    {
        _waveOut?.Play();
    }

    public void Pause()
    {
        _waveOut?.Pause();
    }

    public void Stop()
    {
        if (_waveOut != null)
        {
            _waveOut.Stop();
            if (_audioFileReader != null)
            {
                _audioFileReader.CurrentTime = TimeSpan.Zero;
            }
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    private void CleanupTempFile()
    {
        // Clean up temp file if it exists
        if (_tempFilePath != null && File.Exists(_tempFilePath))
        {
            try
            {
                File.Delete(_tempFilePath);
            }
            catch
            {
                // Schedule for later deletion if file is locked
                try
                {
                    File.Move(_tempFilePath, _tempFilePath + ".delete");
                }
                catch
                {
                    // Ignore if we can't even rename it
                }
            }
            _tempFilePath = null;
        }
    }

    public void Dispose()
    {
        // Stop playback first
        _waveOut?.Stop();
        
        // Dispose in proper order to release file locks
        if (_waveOut != null)
        {
            _waveOut.Dispose();
            _waveOut = null;
        }
        
        if (_audioFileReader != null)
        {
            _audioFileReader.Dispose();
            _audioFileReader = null;
        }
        
        // Now clean up temp file after all locks are released
        CleanupTempFile();
    }
}
