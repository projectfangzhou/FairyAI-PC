using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MyAiAssistant.Services;

public class MiMoAsrService : IMiMoAsrService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public async Task<string> TranscribeAsync(byte[] audioData, string format = "wav")
    {
        Log($"MiMo ASR: audio size={audioData.Length} bytes, format={format}");

        var config = ConfigManager.Load();
        if (string.IsNullOrWhiteSpace(config.ASR.ApiKey))
        {
            Log("MiMo ASR: no API key configured");
            return "";
        }

        try
        {
            using var content = new MultipartFormDataContent();
            var audioContent = new ByteArrayContent(audioData);
            audioContent.Headers.ContentType = new MediaTypeHeaderValue($"audio/{format}");
            content.Add(audioContent, "file", $"audio.{format}");
            content.Add(new StringContent("mimo-v2.5-asr"), "model");

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.xiaomimimo.com/v1/audio/transcriptions")
            {
                Content = content
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ASR.ApiKey);

            using var resp = await Http.SendAsync(req);

            if (!resp.IsSuccessStatusCode)
            {
                var error = await resp.Content.ReadAsStringAsync();
                Log($"MiMo ASR error {resp.StatusCode}: {error}");
                return "";
            }

            var body = await resp.Content.ReadAsStringAsync();
            Log($"MiMo ASR response: {body}");

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("text", out var text))
            {
                var result = text.GetString() ?? "";
                Log($"MiMo ASR result: '{result}'");
                return result;
            }

            return body;
        }
        catch (Exception ex)
        {
            Log($"MiMo ASR exception: {ex.Message}");
            return "";
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }
}
