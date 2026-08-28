using MyAiAssistant.Models;

namespace MyAiAssistant.Services;

public interface ILlmService
{
    IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<ChatMessage> history,
        string userPrompt,
        string endpoint,
        string model,
        string? apiKey = null,
        CancellationToken ct = default);

    /// <summary>Stream chat with automatic fallback to backup model on error.</summary>
    IAsyncEnumerable<string> StreamChatWithFallbackAsync(
        IEnumerable<ChatMessage> history,
        string userPrompt,
        CancellationToken ct = default);
}
