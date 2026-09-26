using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace MyAiAssistant.Services;

/// <summary>
/// Model test service — verifies that the configured LLM API actually works
/// during installation. User cannot proceed until test passes.
/// </summary>
public class ModelTestService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public record TestResult(bool Success, string Message, long LatencyMs);

    /// <summary>Test the LLM endpoint with a simple prompt. Returns success/failure.</summary>
    public async Task<TestResult> TestLlmAsync(string baseUrl, string model, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return new TestResult(false, "Base URL 为空", 0);
        if (string.IsNullOrWhiteSpace(model))
            return new TestResult(false, "模型名称为空", 0);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                model = model,
                messages = new[] { new { role = "user", content = "Hi" } },
                max_tokens = 5
            });

            var req = new HttpRequestMessage(HttpMethod.Post, baseUrl)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(apiKey))
                req.Headers.Add("Authorization", $"Bearer {apiKey}");

            using var resp = await Http.SendAsync(req);
            sw.Stop();

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                var error = body.Length > 100 ? body[..100] : body;
                return new TestResult(false, $"HTTP {(int)resp.StatusCode}: {error}", sw.ElapsedMilliseconds);
            }

            var responseText = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseText);

            // Verify response has content
            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                return new TestResult(true, $"连接成功 (模型: {model})", sw.ElapsedMilliseconds);
            }

            return new TestResult(false, "响应格式异常: 缺少 choices", sw.ElapsedMilliseconds);
        }
        catch (TaskCanceledException)
        {
            return new TestResult(false, "连接超时 (15秒)", sw.ElapsedMilliseconds);
        }
        catch (HttpRequestException ex)
        {
            return new TestResult(false, $"网络错误: {ex.Message}", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return new TestResult(false, $"错误: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    /// <summary>Test vision model (multimodal).</summary>
    public async Task<TestResult> TestVisionAsync(string baseUrl, string model, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(model))
            return new TestResult(false, "配置不完整", 0);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // 1x1 red pixel PNG
            var tinyPng = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/58BAAgEKv/wM2H4AAAAAElFTkSuQmCC");

            var payload = JsonSerializer.Serialize(new
            {
                model = model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = "What color is this image?" },
                            new { type = "image_url", image_url = new { url = $"data:image/png;base64,{Convert.ToBase64String(tinyPng)}" } }
                        }
                    }
                },
                max_tokens = 10
            });

            var req = new HttpRequestMessage(HttpMethod.Post, baseUrl)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(apiKey))
                req.Headers.Add("Authorization", $"Bearer {apiKey}");

            using var resp = await Http.SendAsync(req);
            sw.Stop();

            if (resp.IsSuccessStatusCode)
                return new TestResult(true, $"视觉模型连接成功 (模型: {model})", sw.ElapsedMilliseconds);

            var body = await resp.Content.ReadAsStringAsync();
            return new TestResult(false, $"HTTP {(int)resp.StatusCode}: {body[..Math.Min(100, body.Length)]}", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return new TestResult(false, $"错误: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    /// <summary>Test TTS endpoint.</summary>
    public async Task<TestResult> TestTtsAsync(string baseUrl, string model, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return new TestResult(true, "系统语音无需测试", 0); // system TTS passes

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                model = model,
                input = "test",
                voice = "alloy"
            });

            var req = new HttpRequestMessage(HttpMethod.Post, baseUrl)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(apiKey))
                req.Headers.Add("Authorization", $"Bearer {apiKey}");

            using var resp = await Http.SendAsync(req);
            sw.Stop();

            if (resp.IsSuccessStatusCode)
                return new TestResult(true, $"TTS连接成功 (模型: {model})", sw.ElapsedMilliseconds);

            return new TestResult(false, $"HTTP {(int)resp.StatusCode}", sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return new TestResult(false, $"错误: {ex.Message}", sw.ElapsedMilliseconds);
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [TEST] {msg}\n"); } catch { }
    }
}
