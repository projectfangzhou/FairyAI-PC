using System.Diagnostics;
using System.Globalization;
using System.Speech.Recognition;

namespace MyAiAssistant.Services;

public class ContinuousSpeechService : IContinuousSpeechService
{
    private SpeechRecognitionEngine? _engine;
    private bool _isListening;
    private static readonly string LogPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public event Action<string>? SpeechRecognized;
    public event Action? SpeechEnded;
    public event Action<float>? AudioLevel;

    public void StartListening()
    {
        if (_isListening) return;
        try
        {
            // Use zh-CN recognizer for Chinese dictation
            var devices = SpeechRecognitionEngine.InstalledRecognizers();
            var zhCN = devices.FirstOrDefault(r => r.Culture.Name == "zh-CN");

            if (zhCN != null)
            {
                _engine = new SpeechRecognitionEngine(zhCN.Id);
                Log($"Continuous: using zh-CN recognizer");
            }
            else
            {
                _engine = new SpeechRecognitionEngine();
                Log($"Continuous: using default recognizer");
            }

            _engine.LoadGrammar(new DictationGrammar());
            _engine.SetInputToDefaultAudioDevice();

            _engine.SpeechRecognized += OnSpeechRecognized;
            _engine.SpeechHypothesized += OnSpeechHypothesized;
            _engine.AudioLevelUpdated += OnAudioLevelUpdated;
            _engine.RecognizeCompleted += OnRecognizeCompleted;
            _engine.SpeechRecognitionRejected += OnSpeechRejected;

            _engine.RecognizeAsync(RecognizeMode.Single);
            _isListening = true;
            Log("Continuous: listening started");
        }
        catch (Exception ex)
        {
            Log($"Continuous: init FAILED: {ex.Message}");
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
        Log($"Continuous: recognized '{text}' (confidence={e.Result.Confidence:F2})");

        if (text.Length < 2) { RestartIfNeeded(); return; }

        if (text.Contains("exit", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("quit", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("close", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("退出") ||
            text.Contains("关闭"))
        {
            SpeechEnded?.Invoke();
            return;
        }

        SpeechRecognized?.Invoke(text);
        RestartIfNeeded();
    }

    private void OnSpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
    {
        Log($"Continuous: hypothesized '{e.Result?.Text}'");
    }

    private void OnSpeechRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
    {
        Log($"Continuous: rejected '{e.Result?.Text}'");
        RestartIfNeeded();
    }

    private void OnAudioLevelUpdated(object? sender, AudioLevelUpdatedEventArgs e)
    {
        AudioLevel?.Invoke(e.AudioLevel / 100f);
    }

    private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
    {
        Log($"Continuous: RecognizeCompleted (result={e.Result?.Text ?? "null"}, error={e.Error?.Message ?? "none"})");
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
            _engine.SpeechHypothesized -= OnSpeechHypothesized;
            _engine.AudioLevelUpdated -= OnAudioLevelUpdated;
            _engine.RecognizeCompleted -= OnRecognizeCompleted;
            _engine.SpeechRecognitionRejected -= OnSpeechRejected;
            _engine.Dispose();
        }
    }

    private static void Log(string msg)
    {
        try { System.IO.File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }
}
