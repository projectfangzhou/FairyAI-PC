namespace MyAiAssistant.Services;

public class LLMProviderConfig
{
    public string Name { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string Model { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public bool IsDefault { get; set; }
}

public static class LLMProviders
{
    // Only URL/Model presets — API Keys come from config.json
    public static readonly List<LLMProviderConfig> Presets =
    [
        new() { Name = "Kimi (月之暗面)", BaseUrl = "https://api.moonshot.cn/v1/chat/completions", Model = "kimi-k2.6", IsDefault = true },
        new() { Name = "DeepSeek", BaseUrl = "https://api.deepseek.com/v1/chat/completions", Model = "deepseek-chat" },
        new() { Name = "MiMo (小米)", BaseUrl = "https://api.xiaomimimo.com/v1/chat/completions", Model = "mimo-v2.5" },
        new() { Name = "OpenAI", BaseUrl = "https://api.openai.com/v1/chat/completions", Model = "gpt-4o-mini" },
        new() { Name = "Ollama (本地)", BaseUrl = "http://localhost:11434/v1/chat/completions", Model = "llama3" },
    ];
}
