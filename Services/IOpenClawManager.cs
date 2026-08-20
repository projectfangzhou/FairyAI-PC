namespace MyAiAssistant.Services;

public interface IOpenClawManager : IDisposable
{
    void Start();
    void Stop();
    bool IsRunning { get; }
}
