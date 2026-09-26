using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace MyAiAssistant.Services;

/// <summary>
/// GPT-SoVITS voice synthesis. Connects to a local GPT-SoVITS API server
/// (typically http://localhost:9880) with voice clone reference audio.
/// </summary>
public class GptSoVitsTtsService : IMiMoTtsService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public async Task<byte[]> SynthesizeAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<byte>();

        var config = ConfigManager.Load();
        var tts = config.TTS;
        var baseUrl = string.IsNullOrWhiteSpace(tts.BaseUrl) ? "http://localhost:9880/tts" : tts.BaseUrl;

        if (string.IsNullOrWhiteSpace(tts.VoiceCloneAudioPath))
        {
            Log("GPT-SoVITS: no voice clone audio path configured");
            return Array.Empty<byte>();
        }

        if (!File.Exists(tts.VoiceCloneAudioPath))
        {
            Log($"GPT-SoVITS: reference audio not found: {tts.VoiceCloneAudioPath}");
            return Array.Empty<byte>();
        }

        try
        {
            Log($"GPT-SoVITS: synthesizing '{text[..Math.Min(40, text.Length)]}...' with ref: {Path.GetFileName(tts.VoiceCloneAudioPath)}");

            // Auto-slice reference audio if longer than 30 seconds
            var refInfo = new FileInfo(tts.VoiceCloneAudioPath);
            if (refInfo.Length > 2_000_000) // > ~2MB suggests long audio
            {
                var slicer = new AudioSlicer();
                var chunks = await slicer.SliceAsync(tts.VoiceCloneAudioPath, 10.0);
                if (chunks.Count > 0)
                {
                    Log($"GPT-SoVITS: auto-sliced ref into {chunks.Count} chunks, using first chunk");
                    // Use first chunk as reference for synthesis
                    tts.VoiceCloneAudioPath = chunks[0];
                }
            }

            // Read reference audio as base64
            var refAudioBytes = await File.ReadAllBytesAsync(tts.VoiceCloneAudioPath);
            var refAudioBase64 = Convert.ToBase64String(refAudioBytes);

            var payload = JsonSerializer.Serialize(new
            {
                text = text,
                text_lang = tts.VoiceCloneLang,
                ref_audio_path = tts.VoiceCloneAudioPath,
                prompt_text = string.IsNullOrWhiteSpace(tts.VoiceClonePromptText) ? null : tts.VoiceClonePromptText,
                prompt_lang = tts.VoiceCloneLang,
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, baseUrl)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(tts.ApiKey))
                req.Headers.Add("Authorization", $"Bearer {tts.ApiKey}");

            using var resp = await Http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                var error = await resp.Content.ReadAsStringAsync();
                Log($"GPT-SoVITS error {resp.StatusCode}: {error}");
                return Array.Empty<byte>();
            }

            var audioBytes = await resp.Content.ReadAsByteArrayAsync();

            // If response is JSON with audio field, extract it
            if (audioBytes.Length > 0 && audioBytes[0] == (byte)'{')
            {
                var body = Encoding.UTF8.GetString(audioBytes);
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("audio", out var audioEl))
                {
                    var audioStr = audioEl.GetString();
                    if (!string.IsNullOrEmpty(audioStr))
                        audioBytes = Convert.FromBase64String(audioStr);
                }
            }

            // Convert to WAV if needed
            if (audioBytes.Length > 0 && !IsWav(audioBytes))
            {
                var converter = new FfmpegAudioConverter();
                if (converter.IsAvailable)
                {
                    audioBytes = await converter.ConvertToWavAsync(audioBytes);
                }
            }

            Log($"GPT-SoVITS: generated {audioBytes.Length} bytes");
            return audioBytes;
        }
        catch (Exception ex)
        {
            Log($"GPT-SoVITS exception: {ex.Message}");
            return Array.Empty<byte>();
        }
    }

    private static bool IsWav(byte[] data)
    {
        return data.Length > 4 && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F';
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }
}
