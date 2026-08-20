using MyAiAssistant.Models;

namespace MyAiAssistant.Services;

public interface IChatHistoryService
{
    Task InitializeAsync();
    Task SaveAsync(string sessionId, ChatMessage message);
    Task<List<ChatMessage>> LoadAsync(string sessionId);
}
