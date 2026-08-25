using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MyAiAssistant.Services;

public interface IVoicePipeline : IDisposable
{
    event Action<string>? TranscriptReceived;
    event Action<float>? AudioLevel;
    event Action? ListeningStarted;
    event Action? ListeningStopped;
    void StartListening();
    void StopListening();
}

public class VoicePipeline : IVoicePipeline
{
    private readonly IMiMoAsrService _asr;
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public event Action<string>? TranscriptReceived;
    public event Action<float>? AudioLevel;
    public event Action? ListeningStarted;
    public event Action? ListeningStopped;

    private bool _isListening;

    public VoicePipeline(IMiMoAsrService asr)
    {
        _asr = asr;
    }

    public void StartListening()
    {
        if (_isListening) return;
        _isListening = true;
        ListeningStarted?.Invoke();
        Log("Voice pipeline: listening started");
    }

    public void StopListening()
    {
        _isListening = false;
        ListeningStopped?.Invoke();
        Log("Voice pipeline: listening stopped");
    }

    // Called when audio chunk is captured from system speech recognition
    public async Task ProcessAudioAsync(byte[] audioData)
    {
        if (!_isListening || audioData.Length == 0) return;

        Log($"Voice pipeline: processing {audioData.Length} bytes of audio");

        try
        {
            // Send to MiMo ASR API
            var transcript = await _asr.TranscribeAsync(audioData);

            if (!string.IsNullOrWhiteSpace(transcript))
            {
                Log($"Voice pipeline: transcript='{transcript}'");
                TranscriptReceived?.Invoke(transcript);
            }
        }
        catch (Exception ex)
        {
            Log($"Voice pipeline error: {ex.Message}");
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }

    public void Dispose()
    {
        StopListening();
    }
}
