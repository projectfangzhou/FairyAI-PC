namespace MyAiAssistant.Services;

/// <summary>
/// OpenRouter model provider — unified multimodal (text + vision + speech) endpoint.
/// </summary>
public class OpenRouterService
{
    private static readonly string LogPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public static readonly Dictionary<string, (string BaseUrl, string Model)> Presets = new()
    {
        ["OpenRouter (文本)"] = ("https://openrouter.ai/api/v1/chat/completions", "openai/gpt-4o-mini"),
        ["OpenRouter (视觉)"] = ("https://openrouter.ai/api/v1/chat/completions", "openai/gpt-4o-mini"),
        ["OpenRouter (语音)"] = ("https://openrouter.ai/api/v1/audio/speech", "openai/tts-1"),
    };

    public static readonly string ChatEndpoint = "https://openrouter.ai/api/v1/chat/completions";
    public static readonly string SpeechEndpoint = "https://openrouter.ai/api/v1/audio/speech";

    /// <summary>Get OpenRouter-specific headers.</summary>
    public static Dictionary<string, string> GetHeaders(string apiKey)
    {
        return new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {apiKey}",
            ["HTTP-Referer"] = "https://github.com/projectfangzhou/FairyAI-by-fangzhou",
            ["X-Title"] = "FairyAI",
        };
    }
}
