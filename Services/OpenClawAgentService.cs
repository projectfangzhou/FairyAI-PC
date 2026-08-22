using System.Diagnostics;
using System.IO;
using System.Text;

namespace MyAiAssistant.Services;

public class OpenClawAgentService : IOpenClawAgentService
{
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
    private readonly IAppMappingService _mapping;

    public bool IsAvailable => true;

    public OpenClawAgentService(IAppMappingService mapping)
    {
        _mapping = mapping;
    }

    public async Task<AgentResult> ExecuteAsync(string userRequest)
    {
        Log($"Agent request: '{userRequest}'");
        var lower = userRequest.ToLowerInvariant();

        if (lower.Contains("打开") || lower.Contains("open"))
            return await HandleOpenCommand(userRequest);

        if (lower.Contains("查找") || lower.Contains("搜索") || lower.Contains("search") || lower.Contains("find"))
            return await HandleSearchCommand(userRequest);

        return new AgentResult { Message = "抱歉，我暂时无法处理这个操作。" };
    }

    public async Task OpenAppAsync(string exePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            });
            Log($"Opened: {exePath}");
        }
        catch (Exception ex)
        {
            Log($"Open failed: {ex.Message}");
        }
    }

    private async Task<AgentResult> HandleOpenCommand(string request)
    {
        var lower = request.ToLowerInvariant();

        // Built-in apps
        var appMappings = new Dictionary<string, string>
        {
            ["文件管理器"] = "explorer.exe", ["文件夹"] = "explorer.exe", ["资源管理器"] = "explorer.exe",
            ["计算器"] = "calc.exe", ["记事本"] = "notepad.exe", ["画图"] = "mspaint.exe",
            ["终端"] = "cmd.exe", ["命令行"] = "cmd.exe", ["设置"] = "ms-settings:",
            ["浏览器"] = "msedge", ["edge"] = "msedge", ["chrome"] = "chrome",
            ["word"] = "winword", ["excel"] = "excel", ["powerpoint"] = "powerpnt",
            ["vscode"] = "code", ["visual studio"] = "devenv",
        };

        // 1. Check known apps
        foreach (var kvp in appMappings)
        {
            if (lower.Contains(kvp.Key))
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = kvp.Value, UseShellExecute = true });
                    return new AgentResult { Message = $"已打开 {kvp.Key}。" };
                }
                catch (Exception ex)
                {
                    return new AgentResult { Message = $"打开 {kvp.Key} 失败: {ex.Message}" };
                }
            }
        }

        // 2. Check learned database
        var keyword = ExtractKeyword(request);
        if (keyword != null)
        {
            var savedPath = await _mapping.FindAppAsync(keyword);
            if (savedPath != null && (File.Exists(savedPath) || savedPath.Contains(':')))
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = savedPath, UseShellExecute = true });
                    return new AgentResult { Message = $"已打开 {keyword}（来自学习记录）。" };
                }
                catch { }
            }
        }

        // 3. Search for unknown app
        if (keyword != null)
        {
            var candidates = await _mapping.SearchExeAsync(keyword);
            if (candidates.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"找到以下与 \"{keyword}\" 相关的程序：");
                for (int i = 0; i < candidates.Count; i++)
                {
                    sb.AppendLine($"{i + 1}. {Path.GetFileName(candidates[i])} ({candidates[i]})");
                }
                sb.AppendLine("请告诉我编号，我来打开并记住它。");

                return new AgentResult
                {
                    Message = sb.ToString(),
                    Candidates = candidates,
                    PendingKeyword = keyword
                };
            }
        }

        // 4. Try as file path
        var path = ExtractPath(request);
        if (path != null && (File.Exists(path) || Directory.Exists(path)))
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                return new AgentResult { Message = $"已打开: {path}" };
            }
            catch (Exception ex)
            {
                return new AgentResult { Message = $"打开失败: {ex.Message}" };
            }
        }

        return new AgentResult { Message = $"未找到 \"{keyword ?? "未知应用"}\"，请告诉我具体的应用名称。" };
    }

    private async Task<AgentResult> HandleSearchCommand(string request)
    {
        var keyword = request
            .Replace("查找", "").Replace("搜索", "").Replace("search", "").Replace("find", "")
            .Replace("文件", "").Replace("file", "").Trim();

        if (string.IsNullOrWhiteSpace(keyword))
            return new AgentResult { Message = "请告诉我你要搜索什么。" };

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"search-ms:query={keyword}",
                UseShellExecute = true
            });
            return new AgentResult { Message = $"已打开 Windows 搜索: {keyword}" };
        }
        catch (Exception ex)
        {
            return new AgentResult { Message = $"搜索失败: {ex.Message}" };
        }
    }

    private static string? ExtractKeyword(string request)
    {
        // Extract the app name after "打开"
        var match = System.Text.RegularExpressions.Regex.Match(request, @"(?:打开|open)\s*(.+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var keyword = match.Groups[1].Value.Trim();
            // Remove common suffixes
            keyword = keyword.Replace("一下", "").Replace("吧", "").Trim();
            if (keyword.Length > 0 && keyword.Length <= 20)
                return keyword;
        }
        return null;
    }

    private static string? ExtractPath(string request)
    {
        var words = request.Split(' ', '：', ':', '"', '"');
        foreach (var word in words)
        {
            var trimmed = word.Trim();
            if (Path.IsPathRooted(trimmed) || trimmed.Contains('\\') || trimmed.Contains('/'))
                return trimmed;
        }
        return null;
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }
}
