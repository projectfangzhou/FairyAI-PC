namespace MyAiAssistant.Services;

public interface ITavilySearchService
{
    Task<string> SearchAsync(string query, int maxResults = 5);
}
