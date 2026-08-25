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
    public static readonly List<LLMProviderConfig> Presets =
    [
        new() { Name = "Kimi (月之暗面)", BaseUrl = "https://api.moonshot.cn/v1/chat/completions", Model = "kimi-k2.6", ApiKey = "sk-AJjPEqZImXxDtZwGg4PwmSy2WBuaTbbs4o8mTHlx4VoqBcIv", IsDefault = true },
        new() { Name = "DeepSeek", BaseUrl = "https://api.deepseek.com/v1/chat/completions", Model = "deepseek-chat", ApiKey = "sk-485864714ef54a6aa1a08e6066a44d3e" },
        new() { Name = "MiMo (小米)", BaseUrl = "https://api.xiaomimimo.com/v1/chat/completions", Model = "mimo-v2.5", ApiKey = "sk-c1n1701ggtlme1hi4uai5r8bh0t76urmwwiq7r93v349dee1" },
        new() { Name = "OpenAI", BaseUrl = "https://api.openai.com/v1/chat/completions", Model = "gpt-4o-mini" },
        new() { Name = "Ollama (本地)", BaseUrl = "http://localhost:11434/v1/chat/completions", Model = "llama3" },
    ];
}
