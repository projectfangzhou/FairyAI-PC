namespace MyAiAssistant.Services;

public interface IOpenClawAgentService
{
    Task<string> ExecuteAsync(string userRequest);
    bool IsAvailable { get; }
}
