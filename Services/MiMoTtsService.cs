using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
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
            else
            {
                Log($"TTS: voice sample not found at {VoiceSamplePath}");
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
        if (_voiceSampleBase64 == null)
        {
            Log("TTS: no voice sample available");
            return Array.Empty<byte>();
        }

        Log($"TTS: synthesizing '{text[..Math.Min(50, text.Length)]}...'");

        try
        {
            // Build request: user message contains the voice clone audio, assistant message contains text to speak
            var messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "input_audio",
                            input_audio = new
                            {
                                data = _voiceSampleBase64,
                                format = "wav"
                            }
                        }
                    }
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
                stream = false
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("api-key", ApiKey);

            using var resp = await Http.SendAsync(req);

            if (!resp.IsSuccessStatusCode)
            {
                var error = await resp.Content.ReadAsStringAsync();
                Log($"TTS error {resp.StatusCode}: {error}");
                return Array.Empty<byte>();
            }

            var body = await resp.Content.ReadAsStringAsync();
            Log($"TTS response length: {body.Length}");

            // Parse response to extract audio data
            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var content))
                {
                    // Check if content contains audio data (base64)
                    var contentStr = content.GetString();
                    if (!string.IsNullOrEmpty(contentStr) && contentStr.Length > 100)
                    {
                        // Might be base64 audio
                        try
                        {
                            var audioBytes = Convert.FromBase64String(contentStr);
                            Log($"TTS: audio decoded ({audioBytes.Length} bytes)");
                            return audioBytes;
                        }
                        catch { }
                    }
                }

                // Check for audio field in response
                if (choice.TryGetProperty("audio", out var audio) &&
                    audio.TryGetProperty("data", out var audioData))
                {
                    var audioStr = audioData.GetString();
                    if (!string.IsNullOrEmpty(audioStr))
                    {
                        var audioBytes = Convert.FromBase64String(audioStr);
                        Log($"TTS: audio from 'audio.data' ({audioBytes.Length} bytes)");
                        return audioBytes;
                    }
                }
            }

            Log("TTS: no audio data found in response");
            return Array.Empty<byte>();
        }
        catch (Exception ex)
        {
            Log($"TTS exception: {ex.Message}");
            return Array.Empty<byte>();
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }
}
