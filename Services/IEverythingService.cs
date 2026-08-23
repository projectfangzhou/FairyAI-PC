namespace MyAiAssistant.Services;

public interface IEverythingService
{
    bool IsAvailable { get; }
    List<string> SearchFiles(string query, int maxResults = 10);
}
