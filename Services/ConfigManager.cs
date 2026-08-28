using System.IO;
using System.Text.Json;

namespace MyAiAssistant.Services;

public class AppConfig
{
    public LLMConfig LLM { get; set; } = new();
    public LLMConfig FallbackLLM { get; set; } = new();
    public ASRConfig ASR { get; set; } = new();
    public TTSConfig TTS { get; set; } = new();
    public VisionConfig Vision { get; set; } = new();
    public LocalVisionConfig LocalVision { get; set; } = new();
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
    /// <summary>Additional ASR providers with their endpoints.</summary>
    public Dictionary<string, AsrPreset> Providers { get; set; } = new();
}

public class AsrPreset
{
    public string BaseUrl { get; set; } = "";
    public string Model { get; set; } = "";
}

public class TTSConfig
{
    public string Provider { get; set; } = "MiMo";
    public string ApiKey { get; set; } = "";
    public string Voice { get; set; } = "茉莉";
    public string BaseUrl { get; set; } = "https://api.xiaomimimo.com/v1/audio/speech";
    public string Model { get; set; } = "mi-tts-v2.5";
    /// <summary>Additional TTS providers with their endpoints.</summary>
    public Dictionary<string, TtsPreset> Providers { get; set; } = new();
}

public class TtsPreset
{
    public string BaseUrl { get; set; } = "";
    public string Model { get; set; } = "";
}

public class VisionConfig
{
    public string Provider { get; set; } = "OpenAI";
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/chat/completions";
    public string Model { get; set; } = "gpt-4o-mini";
    /// <summary>Additional vision providers with their endpoints.</summary>
    public Dictionary<string, VisionPreset> Providers { get; set; } = new();
}

public class VisionPreset
{
    public string BaseUrl { get; set; } = "";
    public string Model { get; set; } = "";
    /// <summary>Optional endpoint override for image uploads (e.g. Google files API).</summary>
    public string? ImageEndpoint { get; set; }
}

public class LocalVisionConfig
{
    public bool Enabled { get; set; } = false;
    public string ModelPath { get; set; } = "";
    public string ModelName { get; set; } = "llava-phi3";
    public int MaxTokens { get; set; } = 2048;
    public int Threads { get; set; } = 4;
}

public class TavilyConfig
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.tavily.com/search";
    public string Model { get; set; } = "basic";
    /// <summary>Additional web search providers with their endpoints.</summary>
    public Dictionary<string, WebSearchPreset> Providers { get; set; } = new();
}

public class WebSearchPreset
{
    public string BaseUrl { get; set; } = "";
    public string Model { get; set; } = "";
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

    /// <summary>Visual model (screen understanding) provider presets.</summary>
    public static readonly Dictionary<string, (string BaseUrl, string Model)> VisionPresets = new()
    {
        ["OpenAI"] = ("https://api.openai.com/v1/chat/completions", "gpt-4o-mini"),
        ["Google Gemini"] = ("https://generativelanguage.googleapis.com/v1beta/openai/chat/completions", "gemini-2.0-flash"),
        ["DeepSeek"] = ("https://api.deepseek.com/v1/chat/completions", "deepseek-chat"),
        ["MiMo"] = ("https://api.xiaomimimo.com/v1/chat/completions", "mimo-v2.5"),
        ["Azure OpenAI"] = ("https://{resource}.openai.azure.com/openai/deployments/{deployment}/chat/completions", "gpt-4o-mini"),
    };

    /// <summary>ASR provider presets.</summary>
    public static readonly Dictionary<string, (string BaseUrl, string Model)> AsrPresets = new()
    {
        ["MiMo"] = ("https://api.xiaomimimo.com/v1/audio/transcriptions", "mi-v2.5-asr"),
        ["OpenAI"] = ("https://api.openai.com/v1/audio/transcriptions", "whisper-1"),
        ["Google"] = ("https://speech.googleapis.com/v1/speech:recognize", ""),
        ["Microsoft Azure"] = ("https://{region}.stt.speech.microsoft.com/speech/recognition/conversation", "zh-CN"),
    };

    /// <summary>TTS provider presets.</summary>
    public static readonly Dictionary<string, (string BaseUrl, string Model)> TtsPresets = new()
    {
        ["MiMo"] = ("https://api.xiaomimimo.com/v1/audio/speech", "mi-tts-v2.5"),
        ["OpenAI"] = ("https://api.openai.com/v1/audio/speech", "tts-1"),
        ["Microsoft Azure"] = ("https://{region}.tts.speech.microsoft.com/cognitiveservice/v1", "zh-CN-XiaoxiaoNeural"),
        ["Google"] = ("https://texttospeech.googleapis.com/v1/text:synthesize", "google-tts"),
    };

    /// <summary>Web search provider presets.</summary>
    public static readonly Dictionary<string, (string BaseUrl, string Model)> WebSearchPresets = new()
    {
        ["Tavily"] = ("https://api.tavily.com/search", "basic"),
        ["Google"] = ("https://www.googleapis.com/customsearch/v1", ""),
        ["Bing"] = ("https://api.bing.microsoft.com/v7.0/search", ""),
        ["DuckDuckGo"] = ("https://api.duckduckgo.com", ""),
        ["Baidu"] = ("https://www.baidu.com/s", ""),
    };

    /// <summary>Local vision model presets (for offline use).</summary>
    public static readonly Dictionary<string, (string ModelPath, string ModelName)> LocalVisionPresets = new()
    {
        ["llava-phi3"] = ("models/llava-phi3-Q4_K_M.gguf", "llava-phi3"),
        ["llava-1.5-7b"] = ("models/llava-1.5-7b-Q4_K_M.gguf", "llava-1.5-7b"),
        ["bakllava"] = ("models/bakllava-Q4_K_M.gguf", "bakllava"),
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

    public static (string BaseUrl, string Model)? GetVisionProviderInfo(string providerName)
    {
        return VisionPresets.TryGetValue(providerName, out var info) ? info : null;
    }

    public static (string BaseUrl, string Model)? GetAsrProviderInfo(string providerName)
    {
        return AsrPresets.TryGetValue(providerName, out var info) ? info : null;
    }

    public static (string BaseUrl, string Model)? GetTtsProviderInfo(string providerName)
    {
        return TtsPresets.TryGetValue(providerName, out var info) ? info : null;
    }

    public static List<string> GetVisionProviderNames() => VisionPresets.Keys.ToList();
    public static List<string> GetAsrProviderNames() => AsrPresets.Keys.ToList();
    public static List<string> GetTtsProviderNames() => TtsPresets.Keys.ToList();
    public static List<string> GetLocalVisionModelNames() => LocalVisionPresets.Keys.ToList();

    public static (string ModelPath, string ModelName)? GetLocalVisionModelInfo(string modelName)
    {
        return LocalVisionPresets.TryGetValue(modelName, out var info) ? info : null;
    }

    /// <summary>Check if fallback LLM is configured.</summary>
    public static bool HasFallbackLLM()
    {
        var config = Load();
        return !string.IsNullOrWhiteSpace(config.FallbackLLM.ApiKey) &&
               !string.IsNullOrWhiteSpace(config.FallbackLLM.BaseUrl);
    }

    /// <summary>Get fallback LLM config, returns null if not configured.</summary>
    public static LLMConfig? GetFallbackLLMConfig()
    {
        var config = Load();
        if (string.IsNullOrWhiteSpace(config.FallbackLLM.ApiKey) ||
            string.IsNullOrWhiteSpace(config.FallbackLLM.BaseUrl))
            return null;
        return config.FallbackLLM;
    }
}
