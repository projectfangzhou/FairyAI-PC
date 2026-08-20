namespace MyAiAssistant.Services;

public interface IContinuousSpeechService : IDisposable
{
    event Action<string>? SpeechRecognized;
    event Action? SpeechEnded;
    event Action<float>? AudioLevel;

    void StartListening();
    void StopListening();
}
