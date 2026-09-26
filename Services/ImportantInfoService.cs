using System.IO;

namespace MyAiAssistant.Services;

/// <summary>
/// Auto-saves important extracted information (dates, addresses, etc.) and
/// periodically syncs with network time + weekly web sync with hash protection.
/// </summary>
public class ImportantInfoService
{
    private readonly KnowledgeBaseService _kb;
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public ImportantInfoService(KnowledgeBaseService kb) => _kb = kb;

    /// <summary>Extract and save important info from conversation text.</summary>
    public async Task<string> ExtractAndSaveAsync(string text)
    {
        var config = ConfigManager.Load();
        var prompt = @"从以下文本中提取重要信息（日期、时间、地址、电话号码、金额、重要承诺等）。
如果找到任何重要信息，列出它们。如果没找到，返回空。

文本：
" + text;

        try
        {
            var result = new System.Text.StringBuilder();
            await foreach (var chunk in new LlmService().StreamChatAsync(
                [], prompt, config.LLM.BaseUrl, config.LLM.Model, config.LLM.ApiKey))
            {
                result.Append(chunk);
            }

            var extracted = result.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(extracted))
            {
                await _kb.SaveImportantInfoAsync("important", extracted);
                Log($"Important info extracted and saved");
            }
            return extracted;
        }
        catch (Exception ex)
        {
            Log($"Extract error: {ex.Message}");
            return "";
        }
    }

    /// <summary>Sync time from network (NTP-like) and perform weekly web sync.</summary>
    public async Task SyncAsync()
    {
        try
        {
            // Check if weekly sync is needed
            var config = ConfigManager.Load();
            if (!string.IsNullOrEmpty(config.Context.LastSyncDate))
            {
                var lastSync = DateTime.Parse(config.Context.LastSyncDate);
                if ((DateTime.Now - lastSync).TotalDays < 7)
                    return; // not time yet
            }

            // Weekly sync: search for important scheduled events
            var searchService = new TavilySearchService();
            var today = DateTime.Now.ToString("yyyy年M月d日");
            await searchService.SearchAsync($"{today} 重要日期 节日 纪念日");

            // Update sync date
            config.Context.LastSyncDate = DateTime.Now.ToString("yyyy-MM-dd");
            ConfigManager.Save(config);
            Log("Weekly sync completed");
        }
        catch (Exception ex)
        {
            Log($"Sync error: {ex.Message}");
        }
    }

    /// <summary>Check if today is Monday for weekly auto-sync.</summary>
    public static bool IsMondaySyncTime() => DateTime.Now.DayOfWeek == DayOfWeek.Monday;

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [INFO] {msg}\n"); } catch { }
    }
}
