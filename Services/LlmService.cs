using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MyAiAssistant.Models;

namespace MyAiAssistant.Services;

public class LlmService : ILlmService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public async IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<ChatMessage> history,
        string userPrompt,
        string endpoint,
        string model,
        string? apiKey = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // System prompt for Fairy AI
        var systemMessage = new { role = "system", content = "你是 Fairy，一个友善的 AI 助手。你运行在用户的 Windows 桌面上。你可以回答问题、提供建议、进行闲聊。对于你无法执行的操作（如打开文件、控制系统），请礼貌地说明并提供替代建议。回复简洁自然，像朋友对话一样。" };

        // Filter out empty messages, keep last 10 exchanges to avoid context bloat
        var historyMessages = history
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => new { role = m.Role, content = m.Content })
            .ToList();

        // Limit history to last 20 messages (10 exchanges)
        if (historyMessages.Count > 20)
            historyMessages = historyMessages.Skip(historyMessages.Count - 20).ToList();

        var messages = new List<object> { systemMessage };
        messages.AddRange(historyMessages);

        // Only add userPrompt if it's not empty (to avoid duplicates)
        if (!string.IsNullOrWhiteSpace(userPrompt))
            messages.Add(new { role = "user", content = userPrompt });

        var payload = JsonSerializer.Serialize(new
        {
            model = string.IsNullOrWhiteSpace(model) ? "llama3" : model,
            messages,
            stream = true
        });

        Log($"LLM Request: endpoint={endpoint}, model={model}, messages={messages.Count}");
        Log($"LLM Payload: {payload}");

        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var errorBody = await resp.Content.ReadAsStringAsync(ct);
            Log($"LLM ERROR {resp.StatusCode}: {errorBody}");
            yield break;
        }

        Log("LLM Response OK, streaming...");

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        int chunkCount = 0;
        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..].Trim();
            if (data == "[DONE]") { Log("LLM [DONE] received"); break; }

            string? text = null;
            string? reasoning = null;
            try
            {
                using var doc = JsonDocument.Parse(data);
                if (doc.RootElement.GetProperty("choices").GetArrayLength() > 0)
                {
                    var delta = doc.RootElement.GetProperty("choices")[0].GetProperty("delta");
                    if (delta.TryGetProperty("content", out var c))
                        text = c.GetString();
                    if (delta.TryGetProperty("reasoning_content", out var r))
                        reasoning = r.GetString();
                }
            }
            catch { }

            chunkCount++;
            if (chunkCount <= 5 || !string.IsNullOrEmpty(text))
                Log($"Chunk #{chunkCount}: content='{text}' reasoning='{reasoning?.Substring(0, Math.Min(30, reasoning?.Length ?? 0))}'");

            if (!string.IsNullOrEmpty(text))
                yield return text;
        }
        Log($"LLM streaming done, {chunkCount} chunks total");
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }
}
