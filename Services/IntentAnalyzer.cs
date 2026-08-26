using System.Text;
using System.Text.Json;

namespace MyAiAssistant.Services;

public class IntentResult
{
    public string Intent { get; set; } = "chat"; // chat, open_app, search_file, search_web, open_url
    public string Query { get; set; } = "";
    public string Reply { get; set; } = "";
}

public interface IIntentAnalyzer
{
    Task<IntentResult> AnalyzeAsync(string userMessage, string conversationHistory);
}

public class IntentAnalyzer : IIntentAnalyzer
{
    private readonly ILlmService _llm;
    private static readonly string LogPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public IntentAnalyzer(ILlmService llm)
    {
        _llm = llm;
    }

    public async Task<IntentResult> AnalyzeAsync(string userMessage, string conversationHistory)
    {
        Log($"Intent analysis: '{userMessage}'");

        var systemPrompt = @"你是一个意图分析器。分析用户的消息，判断用户想做什么。

返回一个JSON对象，包含以下字段：
- intent: 可选值为 chat(普通聊天/问答), open_app(打开应用/程序), search_file(搜索本地文件), search_web(联网搜索)
- query: 如果是 open_app，提取应用名称；如果是 search_file，提取搜索关键词；如果是 search_web，提取搜索词；如果是 chat，留空
- reply: 如果是 chat，生成一个简短的回复；其他 intent 留空

注意：
- 打开xxx 且 xxx 是已知软件名 → open_app
- 打开xxx 且 xxx 是文件名/歌曲名/文档名 → search_file
- 查找/搜索xxx → search_file
- 今天天气/最新新闻/实时信息 → search_web
- 你好/谢谢/闲聊 → chat
- 只返回JSON，不要其他内容";

        var fullPrompt = $"{systemPrompt}\n\n[对话历史]\n{conversationHistory}\n\n[用户消息]\n{userMessage}";

        var fullResponse = new StringBuilder();
        try
        {
            var config = ConfigManager.Load();
            await foreach (var chunk in _llm.StreamChatAsync([], fullPrompt, config.LLM.BaseUrl, config.LLM.Model, config.LLM.ApiKey))
            {
                fullResponse.Append(chunk);
            }
        }
        catch (Exception ex)
        {
            Log($"Intent analysis LLM error: {ex.Message}");
            return new IntentResult { Intent = "chat", Reply = "我暂时无法理解你的请求。" };
        }

        var response = fullResponse.ToString().Trim();
        Log($"Intent analysis result: {response}");

        // Try to parse JSON response
        try
        {
            // Remove markdown code blocks if present
            var jsonStr = response;
            if (jsonStr.Contains("```"))
            {
                var start = jsonStr.IndexOf('{');
                var end = jsonStr.LastIndexOf('}');
                if (start >= 0 && end > start)
                    jsonStr = jsonStr[start..(end + 1)];
            }

            using var doc = JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;

            var result = new IntentResult
            {
                Intent = root.TryGetProperty("intent", out var intent) ? intent.GetString() ?? "chat" : "chat",
                Query = root.TryGetProperty("query", out var query) ? query.GetString() ?? "" : "",
                Reply = root.TryGetProperty("reply", out var reply) ? reply.GetString() ?? "" : ""
            };

            Log($"Parsed intent: {result.Intent}, query: '{result.Query}'");
            return result;
        }
        catch (Exception ex)
        {
            Log($"Intent JSON parse error: {ex.Message}, raw: {response}");
            // Fallback: treat as chat
            return new IntentResult { Intent = "chat", Reply = response };
        }
    }

    private static void Log(string msg)
    {
        try { System.IO.File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }
}
