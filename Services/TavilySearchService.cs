using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MyAiAssistant.Services;

public class TavilySearchService : ITavilySearchService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string Endpoint = "https://api.tavily.com/search";
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public async Task<string> SearchAsync(string query, int maxResults = 5)
    {
        Log($"Tavily search: '{query}'");

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                query,
                search_depth = "basic",
                max_results = maxResults,
                include_answer = true
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            var config = ConfigManager.Load();
            if (string.IsNullOrWhiteSpace(config.Tavily.ApiKey))
            {
                Log("Tavily: no API key configured, skipping search");
                return "联网搜索未配置，请在设置中填写 Tavily API Key。";
            }
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.Tavily.ApiKey);

            using var resp = await Http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                var error = await resp.Content.ReadAsStringAsync();
                Log($"Tavily error {resp.StatusCode}: {error}");
                return $"搜索失败: {resp.StatusCode}";
            }

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            var result = new StringBuilder();

            // Extract answer if available
            if (doc.RootElement.TryGetProperty("answer", out var answer))
            {
                result.AppendLine($"[搜索摘要] {answer.GetString()}");
                result.AppendLine();
            }

            // Extract search results
            if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
            {
                result.AppendLine("[搜索结果]");
                int i = 1;
                foreach (var item in results.EnumerateArray())
                {
                    var title = item.TryGetProperty("title", out var t) ? t.GetString() : "";
                    var url = item.TryGetProperty("url", out var u) ? u.GetString() : "";
                    var content = item.TryGetProperty("content", out var c) ? c.GetString() : "";

                    result.AppendLine($"{i}. {title}");
                    result.AppendLine($"   链接: {url}");
                    if (!string.IsNullOrEmpty(content))
                        result.AppendLine($"   摘要: {content[..Math.Min(200, content.Length)]}...");
                    result.AppendLine();
                    i++;
                }
            }

            var searchResult = result.ToString();
            Log($"Tavily result length: {searchResult.Length}");
            return searchResult;
        }
        catch (Exception ex)
        {
            Log($"Tavily exception: {ex.Message}");
            return $"搜索异常: {ex.Message}";
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }
}
