namespace MyAiAssistant.Services;

public interface IAppMappingService
{
    Task InitializeAsync();
    Task<string?> FindAppAsync(string keyword);
    Task SaveMappingAsync(string keyword, string exePath);
    Task<List<string>> SearchExeAsync(string keyword);
}
