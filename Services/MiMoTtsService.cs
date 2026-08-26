using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Speech.Synthesis;
using System.Text;
using System.Text.Json;

namespace MyAiAssistant.Services;

public class MiMoTtsService : IMiMoTtsService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private const string Endpoint = "https://api.xiaomimimo.com/v1/chat/completions";
    private const string Model = "mimo-v2.5-tts";

    // Nori style: soft, warm, gentle voice like a cozy indie game character
    private const string Voice = "茉莉";
    private const string StylePrompt = "温柔、轻柔、温暖的语气，像独立游戏中可爱的精灵角色在和朋友说话。语速适中偏慢，带有治愈感。";

    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public async Task<byte[]> SynthesizeAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<byte>();

        Log($"TTS: synthesizing '{text[..Math.Min(40, text.Length)]}...'");

        // Try MiMo TTS API first
        var result = await TryMiMoTtsAsync(text);
        if (result.Length > 0) return result;

        // Fallback to system TTS
        Log("TTS: MiMo failed, using system TTS fallback");
        return await SystemTtsFallbackAsync(text);
    }

    private async Task<byte[]> TryMiMoTtsAsync(string text)
    {
        try
        {
            // MiMo TTS API format:
            // - user role: style instructions (voice description)
            // - assistant role: text to speak
            // - audio object: format + voice
            var messages = new object[]
            {
                new
                {
                    role = "user",
                    content = StylePrompt
                },
                new
                {
                    role = "assistant",
                    content = text
                }
            };

            var payload = JsonSerializer.Serialize(new
            {
                model = Model,
                messages,
                audio = new
                {
                    format = "wav",
                    voice = Voice
                }
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            var config = ConfigManager.Load();
            if (string.IsNullOrWhiteSpace(config.TTS.ApiKey))
            {
                Log("TTS: no API key configured");
                return Array.Empty<byte>();
            }
            req.Headers.Add("api-key", config.TTS.ApiKey);

            using var resp = await Http.SendAsync(req);

            if (!resp.IsSuccessStatusCode)
            {
                var error = await resp.Content.ReadAsStringAsync();
                Log($"TTS MiMo error {resp.StatusCode}: {error}");
                return Array.Empty<byte>();
            }

            var body = await resp.Content.ReadAsStringAsync();
            Log($"TTS MiMo response length: {body.Length}");

            // Parse response to extract audio data
            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];

                // Check message.audio.data
                if (choice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("audio", out var audio) &&
                    audio.TryGetProperty("data", out var audioData))
                {
                    var audioStr = audioData.GetString();
                    if (!string.IsNullOrEmpty(audioStr))
                    {
                        var audioBytes = Convert.FromBase64String(audioStr);
                        Log($"TTS MiMo: audio decoded ({audioBytes.Length} bytes)");
                        return audioBytes;
                    }
                }
            }

            Log("TTS MiMo: no audio found in response");
            return Array.Empty<byte>();
        }
        catch (Exception ex)
        {
            Log($"TTS MiMo exception: {ex.Message}");
            return Array.Empty<byte>();
        }
    }

    private Task<byte[]> SystemTtsFallbackAsync(string text)
    {
        try
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"fairy_tts_{Guid.NewGuid():N}.wav");
            var synth = new SpeechSynthesizer();
            synth.SetOutputToWaveFile(tempFile);
            synth.Speak(text);

            var audioBytes = File.ReadAllBytes(tempFile);
            try { File.Delete(tempFile); } catch { }

            Log($"TTS system: generated {audioBytes.Length} bytes");
            return Task.FromResult(audioBytes);
        }
        catch (Exception ex)
        {
            Log($"TTS system fallback error: {ex.Message}");
            return Task.FromResult(Array.Empty<byte>());
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }
}
