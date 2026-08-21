using System.Diagnostics;
using System.Globalization;
using System.Speech.Recognition;

namespace MyAiAssistant.Services;

public class ContinuousSpeechService : IContinuousSpeechService
{
    private SpeechRecognitionEngine? _engine;
    private bool _isListening;

    public event Action<string>? SpeechRecognized;
    public event Action? SpeechEnded;
    public event Action<float>? AudioLevel;

    public void StartListening()
    {
        if (_isListening) return;
        try
        {
            _engine = new SpeechRecognitionEngine();
            _engine.LoadGrammar(new DictationGrammar());
            _engine.SetInputToDefaultAudioDevice();

            _engine.SpeechRecognized += OnSpeechRecognized;
            _engine.AudioLevelUpdated += OnAudioLevelUpdated;
            _engine.RecognizeCompleted += OnRecognizeCompleted;

            _engine.RecognizeAsync(RecognizeMode.Single);
            _isListening = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Speech] Init failed: {ex.Message}");
        }
    }

    public void StopListening()
    {
        _isListening = false;
        try { _engine?.RecognizeAsyncStop(); } catch { }
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        if (e.Result.Text.Length < 2) return;

        if (e.Result.Text.Contains("exit", StringComparison.OrdinalIgnoreCase) ||
            e.Result.Text.Contains("quit", StringComparison.OrdinalIgnoreCase) ||
            e.Result.Text.Contains("close", StringComparison.OrdinalIgnoreCase))
        {
            SpeechEnded?.Invoke();
            return;
        }

        SpeechRecognized?.Invoke(e.Result.Text.Trim());
        RestartIfNeeded();
    }

    private void OnAudioLevelUpdated(object? sender, AudioLevelUpdatedEventArgs e)
    {
        AudioLevel?.Invoke(e.AudioLevel / 100f);
    }

    private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
    {
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
            _engine.Dispose();
        }
    }
}
