using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MyAiAssistant.Models;

namespace MyAiAssistant.Services;

/// <summary>
/// Wraps LLM streaming with Function Calling support.
/// Sends tools to the LLM, executes any tool calls, feeds results back,
/// and streams the final answer.
/// </summary>
public class FunctionCallingService
{
    private readonly ILlmService _llm;
    private readonly ToolRegistry _tools;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
    private const int MaxToolRounds = 3;

    public bool HasTools => _tools.GetAll().Any();

    public FunctionCallingService(ILlmService llm, ToolRegistry tools)
    {
        _llm = llm;
        _tools = tools;
    }

    /// <summary>
    /// Chat with function calling. If the LLM requests tool calls,
    /// execute them automatically and loop until a final answer is produced.
    /// Returns the final answer via streaming.
    /// </summary>
    public async IAsyncEnumerable<string> ChatWithToolsAsync(
        IEnumerable<ChatMessage> history,
        string userPrompt,
        string endpoint,
        string model,
        string? apiKey = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!HasTools)
        {
            // No tools registered, fall through to plain chat
            await foreach (var chunk in _llm.StreamChatAsync(history, userPrompt, endpoint, model, apiKey, ct))
                yield return chunk;
            yield break;
        }

        // Build initial messages
        var messages = new List<ChatMessage>();
        messages.AddRange(history.Where(m => !string.IsNullOrWhiteSpace(m.Content)).TakeLast(20));
        messages.Add(new ChatMessage { Role = "user", Content = userPrompt });

        for (int round = 0; round < MaxToolRounds; round++)
        {
            // Make non-streaming request to check for tool_calls
            var (content, toolCalls) = await CallLlmWithToolsAsync(messages, endpoint, model, apiKey, ct);

            if (toolCalls == null || toolCalls.Count == 0)
            {
                // No tool calls — this is the final answer, stream it
                foreach (var c in content)
                    yield return c;
                yield break;
            }

            // Add assistant message with tool calls
            var assistantContent = content.Length > 0 ? string.Join(" ", content) : "(calling tools...)";
            messages.Add(new ChatMessage { Role = "assistant", Content = assistantContent });

            // Execute each tool call and add results
            foreach (var tc in toolCalls)
            {
                Log($"Tool call: {tc.Name}({tc.Arguments})");
                var result = await _tools.ExecuteToolCallAsync(tc.Name, tc.Arguments);
                Log($"Tool result: {result[..Math.Min(200, result.Length)]}");

                messages.Add(new ChatMessage { Role = "tool", Content = $"[{tc.Name}] {result}" });
            }
        }

        // If we exhausted rounds, do a final streaming response
        await foreach (var chunk in _llm.StreamChatAsync(
            messages.TakeLast(30).Select(m => new ChatMessage { Role = m.Role, Content = m.Content }),
            "", endpoint, model, apiKey, ct))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Make a single non-streaming call with tools. Returns content + any tool_calls.
    /// </summary>
    private async Task<(string[] Content, List<ToolCall>? ToolCalls)> CallLlmWithToolsAsync(
        List<ChatMessage> messages,
        string endpoint, string model, string? apiKey,
        CancellationToken ct)
    {
        var contentParts = new List<string>();

        var payload = JsonSerializer.Serialize(new
        {
            model = string.IsNullOrWhiteSpace(model) ? "llama3" : model,
            messages = messages.Select(m => new { m.Role, m.Content }).ToList(),
            tools = _tools.GetAll().Select(t => JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = new
                    {
                        type = "object",
                        properties = t.Parameters.ToDictionary(p => p.Name, p => new { type = p.Type, description = p.Description }),
                        required = t.Parameters.Where(p => p.Required).Select(p => p.Name).ToList()
                    }
                }
            }))).ToList(),
            stream = false
        });

        var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(apiKey))
            req.Headers.Add("Authorization", $"Bearer {apiKey}");

        using var resp = await Http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            Log($"LLM error {resp.StatusCode}: {body}");
            return (new[] { $"Error: {resp.StatusCode}" }, null);
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var choice = root.GetProperty("choices")[0];
        var message = choice.GetProperty("message");

        // Content
        if (message.TryGetProperty("content", out var contentEl) && contentEl.ValueKind != JsonValueKind.Null)
        {
            var text = contentEl.GetString() ?? "";
            if (!string.IsNullOrEmpty(text)) contentParts.Add(text);
        }

        // Tool calls
        List<ToolCall>? toolCalls = null;
        if (message.TryGetProperty("tool_calls", out var callsEl) && callsEl.GetArrayLength() > 0)
        {
            toolCalls = new List<ToolCall>();
            foreach (var call in callsEl.EnumerateArray())
            {
                var fn = call.GetProperty("function");
                toolCalls.Add(new ToolCall
                {
                    Id = call.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "",
                    Name = fn.GetProperty("name").GetString() ?? "",
                    Arguments = fn.GetProperty("arguments").GetString() ?? "{}"
                });
            }
        }

        return (contentParts.ToArray(), toolCalls);
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [FC] {msg}\n"); } catch { }
    }

    public class ToolCall
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Arguments { get; set; } = "{}";
    }
}
