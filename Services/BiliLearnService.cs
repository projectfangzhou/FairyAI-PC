using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace MyAiAssistant.Services;

/// <summary>
/// BiliLearn integration — downloads Bilibili videos and stores content in knowledge base.
/// Daily limit: 20 videos. Source: https://github.com/xiaoyaya191/bilibili_learning_bot
/// </summary>
public class BiliLearnService
{
    private readonly YtDlpService _ytDlp;
    private readonly KnowledgeBaseService _kb;
    private const int DailyLimit = 20;
    private static readonly string StatsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bililearn_stats.json");
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public BiliLearnService(YtDlpService ytDlp, KnowledgeBaseService kb)
    {
        _ytDlp = ytDlp;
        _kb = kb;
    }

    /// <summary>Process a Bilibili URL: download + extract to knowledge base.</summary>
    public async Task<string> LearnFromUrlAsync(string url)
    {
        if (!CanLearnToday())
            return $"今日学习已达上限 ({DailyLimit}个视频)。明天再来吧！";

        try
        {
            var downloadDir = Path.Combine(_kb.KbFolder, "bilibili");
            Directory.CreateDirectory(downloadDir);

            Log($"BiliLearn: processing {url}");
            var videoPath = await _ytDlp.DownloadVideoAsync(url, downloadDir);

            if (string.IsNullOrWhiteSpace(videoPath))
                return "视频下载失败，请检查链接。";

            // Extract title and save to knowledge base
            var title = Path.GetFileNameWithoutExtension(videoPath);
            var entry = $"# B站学习: {title}\n\n来源: {url}\n日期: {DateTime.Now:yyyy-MM-dd}\n\n视频已保存: {videoPath}\n";
            await File.WriteAllTextAsync(
                Path.Combine(_kb.KbFolder, $"bililearn_{title}.md"), entry);
            await File.WriteAllTextAsync(
                Path.Combine(_kb.ObsidianVault, $"bililearn_{title}.md"), entry);

            IncrementCount();
            Log($"BiliLearn: saved '{title}' ({GetCountToday()}/{DailyLimit})");
            return $"已学习: {title} ({GetCountToday()}/{DailyLimit})";
        }
        catch (Exception ex)
        {
            Log($"BiliLearn error: {ex.Message}");
            return $"学习失败: {ex.Message}";
        }
    }

    /// <summary>Check if today's limit is reached.</summary>
    public bool CanLearnToday() => GetCountToday() < DailyLimit;

    private int GetCountToday()
    {
        try
        {
            if (!File.Exists(StatsPath)) return 0;
            var stats = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(StatsPath));
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            return stats?.GetValueOrDefault(today, 0) ?? 0;
        }
        catch { return 0; }
    }

    private void IncrementCount()
    {
        try
        {
            var stats = File.Exists(StatsPath)
                ? JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(StatsPath))
                  ?? new Dictionary<string, int>()
                : new Dictionary<string, int>();
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            stats[today] = stats.GetValueOrDefault(today, 0) + 1;
            File.WriteAllText(StatsPath, JsonSerializer.Serialize(stats));
        }
        catch { }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [BILI] {msg}\n"); } catch { }
    }
}
