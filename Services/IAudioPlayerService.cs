namespace MyAiAssistant.Services;

public interface IAudioPlayerService
{
    Task PlayAsync(byte[] audioData);
    void Stop();
}
