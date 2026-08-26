using System.IO;
using System.Text.Json;

namespace MyAiAssistant.Services;

public class AppConfig
{
    public LLMConfig LLM { get; set; } = new();
    public ASRConfig ASR { get; set; } = new();
    public TTSConfig TTS { get; set; } = new();
    public TavilyConfig Tavily { get; set; } = new();
}

public class LLMConfig
{
    public string Provider { get; set; } = "Kimi";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.moonshot.cn/v1/chat/completions";
    public string Model { get; set; } = "kimi-k2.6";
}

public class ASRConfig
{
    public string Provider { get; set; } = "MiMo";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.xiaomimimo.com/v1/audio/transcriptions";
}

public class TTSConfig
{
    public string Provider { get; set; } = "MiMo";
    public string ApiKey { get; set; } = "";
    public string Voice { get; set; } = "茉莉";
}

public class TavilyConfig
{
    public string ApiKey { get; set; } = "";
}

public static class ConfigManager
{
    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "config.json");

    private static readonly Dictionary<string, (string BaseUrl, string Model)> ProviderPresets = new()
    {
        ["Kimi"] = ("https://api.moonshot.cn/v1/chat/completions", "kimi-k2.6"),
        ["DeepSeek"] = ("https://api.deepseek.com/v1/chat/completions", "deepseek-chat"),
        ["MiMo"] = ("https://api.xiaomimimo.com/v1/chat/completions", "mimo-v2.5"),
        ["OpenAI"] = ("https://api.openai.com/v1/chat/completions", "gpt-4o-mini"),
        ["Ollama"] = ("http://localhost:11434/v1/chat/completions", "llama3"),
    };

    public static AppConfig Load()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch { }
        }
        return new AppConfig();
    }

    public static void Save(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    public static bool NeedsSetup()
    {
        if (!File.Exists(ConfigPath)) return true;
        var config = Load();
        return string.IsNullOrWhiteSpace(config.LLM.ApiKey);
    }

    public static (string BaseUrl, string Model)? GetProviderInfo(string providerName)
    {
        return ProviderPresets.TryGetValue(providerName, out var info) ? info : null;
    }

    public static List<string> GetProviderNames() => ProviderPresets.Keys.ToList();
}
