namespace MyAiAssistant.Services;

public interface IOpenClawAgentService
{
    Task<AgentResult> ExecuteAsync(string userRequest);
    Task OpenAppAsync(string exePath);
    bool IsAvailable { get; }
}

public class AgentResult
{
    public string Message { get; set; } = "";
    public List<string>? Candidates { get; set; }
    public string? PendingKeyword { get; set; }
    public bool NeedsConfirmation => Candidates is { Count: > 0 };
}
