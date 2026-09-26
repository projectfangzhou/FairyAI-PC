using System.Text.Json;
using System.Text.Json.Nodes;
using MyAiAssistant.Models;

namespace MyAiAssistant.Services;

/// <summary>
/// Shared conversation context with automatic compression at token threshold.
/// All conversations share one context; compresses every N tokens (default 256K)
/// to balance chat completeness with cost and performance.
/// </summary>
public class ContextCompressionService
{
    private readonly ILlmService _llm;
    private static readonly string LogPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public ContextCompressionService(ILlmService llm) => _llm = llm;

    /// <summary>Check if context should be compressed based on message count/tokens.</summary>
    public bool ShouldCompress(IReadOnlyList<ChatMessage> history)
    {
        var config = ConfigManager.Load();
        if (!config.Context.Enabled) return false;

        // Rough token estimate: ~2 chars per token
        long totalChars = history.Sum(m => m.Content?.Length ?? 0);
        long estimatedTokens = totalChars / 2;
        return estimatedTokens >= config.Context.AutoCompressTokens;
    }

    /// <summary>Compress conversation history to summary. Returns compressed history.</summary>
    public async Task<List<ChatMessage>> CompressAsync(List<ChatMessage> history)
    {
        if (history.Count < 4) return history;

        var config = ConfigManager.Load();

        // Keep last 6 messages as-is, summarize the rest
        var toSummarize = history.Take(history.Count - 6).ToList();
        var recent = history.TakeLast(6).ToList();

        var conversationText = string.Join("\n",
            toSummarize.Select(m => $"{m.Role}: {m.Content}"));

        var summaryPrompt = "请将以下对话历史压缩为简要摘要，保留关键信息（事实、决定、人物、日期、地址等）。摘要不超过200字。\n\n" + conversationText;

        try
        {
            var summary = new System.Text.StringBuilder();
            await foreach (var chunk in _llm.StreamChatAsync(
                [], summaryPrompt, config.LLM.BaseUrl, config.LLM.Model, config.LLM.ApiKey))
            {
                summary.Append(chunk);
            }

            var result = new List<ChatMessage>
            {
                new() { Role = "system", Content = $"[对话摘要] {summary}" }
            };
            result.AddRange(recent);

            Log($"Context compressed: {history.Count} -> {result.Count} messages");
            return result;
        }
        catch (Exception ex)
        {
            Log($"Compression error: {ex.Message}");
            return history; // keep original on failure
        }
    }

    private static void Log(string msg)
    {
        try { System.IO.File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [CTX] {msg}\n"); } catch { }
    }
}
