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
    private const string ApiKey = "sk-c5ztsi49y85kfnj6j6ynfh56wcdlvst5g0i463h1owltxnna";
    private const string Endpoint = "https://api.xiaomimimo.com/v1/chat/completions";
    private const string Model = "mimo-v2.5-tts-voiceclone";
    private static readonly string VoiceSamplePath = @"D:\Dev\tts_1787503081784761800.wav";
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    private string? _voiceSampleBase64;

    public MiMoTtsService()
    {
        LoadVoiceSample();
    }

    private void LoadVoiceSample()
    {
        try
        {
            if (File.Exists(VoiceSamplePath))
            {
                var bytes = File.ReadAllBytes(VoiceSamplePath);
                _voiceSampleBase64 = Convert.ToBase64String(bytes);
                Log($"TTS: voice sample loaded ({bytes.Length} bytes)");
            }
        }
        catch (Exception ex)
        {
            Log($"TTS: failed to load voice sample: {ex.Message}");
        }
    }

    public async Task<byte[]> SynthesizeAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<byte>();

        // Try MiMo TTS API first
        var result = await TryMiMoTtsAsync(text);
        if (result.Length > 0) return result;

        // Fallback to system TTS
        Log("TTS: MiMo failed, using system TTS fallback");
        return await SystemTtsFallbackAsync(text);
    }

    private async Task<byte[]> TryMiMoTtsAsync(string text)
    {
        if (_voiceSampleBase64 == null) return Array.Empty<byte>();

        Log($"TTS: trying MiMo API for '{text[..Math.Min(30, text.Length)]}...'");

        try
        {
            var voiceDataUrl = $"data:audio/wav;base64,{_voiceSampleBase64}";

            var messages = new object[]
            {
                new { role = "user", content = voiceDataUrl },
                new { role = "assistant", content = text }
            };

            var payload = JsonSerializer.Serialize(new
            {
                model = Model,
                messages
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("api-key", ApiKey);

            using var resp = await Http.SendAsync(req);

            if (!resp.IsSuccessStatusCode)
            {
                Log($"TTS MiMo error: {resp.StatusCode}");
                return Array.Empty<byte>();
            }

            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("audio", out var audio) &&
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

                // Check message content for audio
                if (choice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var content))
                {
                    var contentStr = content.GetString();
                    if (!string.IsNullOrEmpty(contentStr) && contentStr.Length > 200)
                    {
                        try
                        {
                            var audioBytes = Convert.FromBase64String(contentStr);
                            Log($"TTS MiMo: audio from content ({audioBytes.Length} bytes)");
                            return audioBytes;
                        }
                        catch { }
                    }
                }
            }

            Log("TTS MiMo: no audio in response");
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
            var synth = new SpeechSynthesizer();
            synth.SetOutputToWaveFile(Path.Combine(Path.GetTempPath(), "fairy_tts_system.wav"));
            synth.Speak(text);

            var audioBytes = File.ReadAllBytes(Path.Combine(Path.GetTempPath(), "fairy_tts_system.wav"));
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
