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

    public async IAsyncEnumerable<string> StreamChatAsync(
        IEnumerable<ChatMessage> history,
        string userPrompt,
        string endpoint,
        string model,
        string? apiKey = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var messages = history.Select(m => new { role = m.Role, content = m.Content }).ToList();
        messages.Add(new { role = "user", content = userPrompt });

        var payload = JsonSerializer.Serialize(new
        {
            model = string.IsNullOrWhiteSpace(model) ? "llama3" : model,
            messages,
            stream = true
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..].Trim();
            if (data == "[DONE]") break;

            string? text = null;
            try
            {
                using var doc = JsonDocument.Parse(data);
                if (doc.RootElement.GetProperty("choices").GetArrayLength() > 0)
                {
                    var delta = doc.RootElement.GetProperty("choices")[0].GetProperty("delta");
                    if (delta.TryGetProperty("content", out var c))
                        text = c.GetString();
                }
            }
            catch { }

            if (!string.IsNullOrEmpty(text))
                yield return text;
        }
    }
}
