using System.Diagnostics;
using System.IO;
using System.Text;

namespace MyAiAssistant.Services;

public class OpenClawAgentService : IOpenClawAgentService
{
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public bool IsAvailable => true; // Local agent is always available

    public async Task<string> ExecuteAsync(string userRequest)
    {
        Log($"Agent request: '{userRequest}'");
        var lower = userRequest.ToLowerInvariant();

        // Open file/folder
        if (lower.Contains("打开") || lower.Contains("open"))
        {
            return await HandleOpenCommand(userRequest);
        }

        // Search files
        if (lower.Contains("查找") || lower.Contains("搜索") || lower.Contains("search") || lower.Contains("find"))
        {
            return await HandleSearchCommand(userRequest);
        }

        // Default: let LLM handle it
        return "抱歉，我暂时无法处理这个操作。";
    }

    private static async Task<string> HandleOpenCommand(string request)
    {
        var lower = request.ToLowerInvariant();

        // Open common apps
        var appMappings = new Dictionary<string, string>
        {
            ["文件管理器"] = "explorer.exe",
            ["文件夹"] = "explorer.exe",
            ["资源管理器"] = "explorer.exe",
            ["计算器"] = "calc.exe",
            ["记事本"] = "notepad.exe",
            ["画图"] = "mspaint.exe",
            ["终端"] = "cmd.exe",
            ["命令行"] = "cmd.exe",
            ["设置"] = "ms-settings:",
            ["浏览器"] = "msedge",
            ["edge"] = "msedge",
            ["chrome"] = "chrome",
            ["word"] = "winword",
            ["excel"] = "excel",
            ["powerpoint"] = "powerpnt",
            ["vscode"] = "code",
            ["visual studio"] = "devenv",
        };

        foreach (var kvp in appMappings)
        {
            if (lower.Contains(kvp.Key))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = kvp.Value,
                        UseShellExecute = true
                    });
                    return $"已打开 {kvp.Key}。";
                }
                catch (Exception ex)
                {
                    return $"打开 {kvp.Key} 失败: {ex.Message}";
                }
            }
        }

        // Try to open as file path
        var path = ExtractPath(request);
        if (path != null && (File.Exists(path) || Directory.Exists(path)))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                return $"已打开: {path}";
            }
            catch (Exception ex)
            {
                return $"打开失败: {ex.Message}";
            }
        }

        return "已收到打开请求，但未识别到具体的应用或文件。";
    }

    private static async Task<string> HandleSearchCommand(string request)
    {
        var keyword = request
            .Replace("查找", "").Replace("搜索", "").Replace("search", "").Replace("find", "")
            .Replace("文件", "").Replace("file", "").Trim();

        if (string.IsNullOrWhiteSpace(keyword))
            return "请告诉我你要搜索什么。";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"search-ms:query={keyword}",
                UseShellExecute = true
            });
            return $"已打开 Windows 搜索: {keyword}";
        }
        catch (Exception ex)
        {
            return $"搜索失败: {ex.Message}";
        }
    }

    private static string? ExtractPath(string request)
    {
        // Try to find a file path in the request
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
