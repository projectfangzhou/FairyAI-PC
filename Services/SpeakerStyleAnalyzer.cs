using System.IO;
using System.Net.Http;
using System.Text.Json;
using MyAiAssistant.Models;

namespace MyAiAssistant.Services;

/// <summary>
/// Speaking style and interest analyzer — builds a profile of each conversation partner
/// based on their messages. No fixed output format; focuses on accuracy.
/// </summary>
public class SpeakerStyleAnalyzer
{
    private readonly ILlmService _llm;
    private readonly string _profilePath;
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public SpeakerStyleAnalyzer(ILlmService llm)
    {
        _llm = llm;
        _profilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles");
        Directory.CreateDirectory(_profilePath);
    }

    /// <summary>Analyze messages and update speaker profile.</summary>
    public async Task<string> AnalyzeAsync(string speakerId, List<ChatMessage> messages)
    {
        if (messages.Count < 3) return "";

        var config = ConfigManager.Load();
        var conversation = string.Join("\n", messages.TakeLast(20)
            .Select(m => $"{m.Role}: {m.Content}"));

        var prompt = @"分析以下对话中说话者的风格和兴趣。关注：
1. 说话风格（正式/随意、幽默/严肃、简洁/详细）
2. 兴趣爱好（从话题中推断）
3. 沟通偏好（喜欢提问/陈述、需要解释/直接给答案）

不要求固定格式，只需准确描述即可。

对话内容：
" + conversation;

        try
        {
            var profile = new System.Text.StringBuilder();
            await foreach (var chunk in _llm.StreamChatAsync(
                [], prompt, config.LLM.BaseUrl, config.LLM.Model, config.LLM.ApiKey))
            {
                profile.Append(chunk);
            }

            var result = profile.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(result))
            {
                var path = Path.Combine(_profilePath, $"{speakerId}.txt");
                await File.AppendAllTextAsync(path,
                    $"\n--- {DateTime.Now:yyyy-MM-dd HH:mm} ---\n{result}\n");
                Log($"Profile updated for {speakerId}");
            }
            return result;
        }
        catch (Exception ex)
        {
            Log($"Speaker analysis error: {ex.Message}");
            return "";
        }
    }

    /// <summary>Get current profile for a speaker.</summary>
    public string GetProfile(string speakerId)
    {
        var path = Path.Combine(_profilePath, $"{speakerId}.txt");
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [SPK] {msg}\n"); } catch { }
    }
}
