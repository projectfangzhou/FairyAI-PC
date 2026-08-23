using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Speech.AudioFormat;
using System.Speech.Recognition;

namespace MyAiAssistant.Services;

public class HybridSpeechService : IContinuousSpeechService
{
    private SpeechRecognitionEngine? _engine;
    private bool _isListening;
    private readonly IMiMoAsrService _mimoAsr;
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    // Audio capture buffer
    private MemoryStream? _audioStream;
    private bool _isCapturingAudio;

    public event Action<string>? SpeechRecognized;
    public event Action? SpeechEnded;
    public event Action<float>? AudioLevel;

    public HybridSpeechService(IMiMoAsrService mimoAsr)
    {
        _mimoAsr = mimoAsr;
    }

    public void StartListening()
    {
        if (_isListening) return;
        try
        {
            // Use zh-CN recognizer for continuous listening + audio capture
            var devices = SpeechRecognitionEngine.InstalledRecognizers();
            var zhCN = devices.FirstOrDefault(r => r.Culture.Name == "zh-CN");

            if (zhCN != null)
                _engine = new SpeechRecognitionEngine(zhCN.Id);
            else
                _engine = new SpeechRecognitionEngine();

            _engine.LoadGrammar(new DictationGrammar());
            _engine.SetInputToDefaultAudioDevice();

            _engine.SpeechRecognized += OnSpeechRecognized;
            _engine.AudioLevelUpdated += OnAudioLevelUpdated;
            _engine.RecognizeCompleted += OnRecognizeCompleted;
            _engine.SpeechRecognitionRejected += OnSpeechRejected;

            _engine.RecognizeAsync(RecognizeMode.Single);
            _isListening = true;
            Log("Hybrid: listening started (zh-CN + MiMo ASR)");
        }
        catch (Exception ex)
        {
            Log($"Hybrid: init FAILED: {ex.Message}");
        }
    }

    public void StopListening()
    {
        _isListening = false;
        try { _engine?.RecognizeAsyncStop(); } catch { }
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        var text = e.Result.Text?.Trim() ?? "";
        Log($"Hybrid: system recognized '{text}' (confidence={e.Result.Confidence:F2})");

        if (text.Length < 2) { RestartIfNeeded(); return; }

        // Check for exit commands
        if (text.Contains("退出") || text.Contains("关闭") ||
            text.Contains("exit", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("quit", StringComparison.OrdinalIgnoreCase))
        {
            SpeechEnded?.Invoke();
            return;
        }

        // Fire system recognition result immediately
        SpeechRecognized?.Invoke(text);

        // Also try MiMo ASR for better accuracy (async, non-blocking)
        _ = TryMiMoAsrAsync();

        RestartIfNeeded();
    }

    private async Task TryMiMoAsrAsync()
    {
        // Note: In a full implementation, we would capture raw audio from the microphone
        // and send it to MiMo ASR API. For now, we use the system result as the primary
        // and rely on MiMo ASR for post-processing if needed.
        //
        // To capture audio, we would need:
        // 1. WaveInEvent to capture from microphone
        // 2. Save to WAV format
        // 3. Send byte[] to MiMo ASR API
        //
        // This adds ~1-2s latency but provides much better accuracy.
        // The system speech result is already sent to the user for immediate feedback.
    }

    private void OnSpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
    {
        // Partial results - could show live transcription
    }

    private void OnAudioLevelUpdated(object? sender, AudioLevelUpdatedEventArgs e)
    {
        AudioLevel?.Invoke(e.AudioLevel / 100f);
    }

    private void OnSpeechRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
    {
        Log($"Hybrid: rejected '{e.Result?.Text}'");
        RestartIfNeeded();
    }

    private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
    {
        Log($"Hybrid: RecognizeCompleted (result={e.Result?.Text ?? "null"})");
        RestartIfNeeded();
    }

    private void RestartIfNeeded()
    {
        if (_isListening && _engine != null)
        {
            try { _engine.RecognizeAsync(RecognizeMode.Single); } catch { }
        }
    }

    public void Dispose()
    {
        StopListening();
        if (_engine != null)
        {
            _engine.SpeechRecognized -= OnSpeechRecognized;
            _engine.AudioLevelUpdated -= OnAudioLevelUpdated;
            _engine.RecognizeCompleted -= OnRecognizeCompleted;
            _engine.SpeechRecognitionRejected -= OnSpeechRejected;
            _engine.Dispose();
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }
}
