using System.Diagnostics;
using System.IO;
using System.Text;

namespace MyAiAssistant.Services;

public class OpenClawAgentService : IOpenClawAgentService
{
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
    private readonly string _openclawPath;

    public bool IsAvailable => _openclawPath != null && File.Exists(_openclawPath);

    public OpenClawAgentService()
    {
        _openclawPath = FindOpenClaw() ?? "";
    }

    public async Task<string> ExecuteAsync(string userRequest)
    {
        if (!IsAvailable)
        {
            Log("OpenClaw not available");
            return "OpenClaw 未安装，无法执行本地操作。";
        }

        Log($"OpenClaw agent: '{userRequest}'");

        try
        {
            // Write message to temp file to avoid encoding issues with Chinese
            var tempFile = Path.Combine(Path.GetTempPath(), $"fairy_agent_{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(tempFile, userRequest, Encoding.UTF8);

            var psi = new ProcessStartInfo
            {
                FileName = _openclawPath,
                Arguments = $"agent --local --message-file \"{tempFile}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(_openclawPath) ?? ""
            };

            using var process = Process.Start(psi);
            if (process == null) return "无法启动 OpenClaw 进程。";

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            // Cleanup temp file
            try { File.Delete(tempFile); } catch { }

            Log($"OpenClaw exit code: {process.ExitCode}");

            if (!string.IsNullOrWhiteSpace(output))
            {
                Log($"OpenClaw output: {output[..Math.Min(200, output.Length)]}");
                return output.Trim();
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                Log($"OpenClaw error: {error[..Math.Min(200, error.Length)]}");
                return $"OpenClaw 执行出错: {error.Trim()}";
            }

            return "OpenClaw 未返回结果。";
        }
        catch (Exception ex)
        {
            Log($"OpenClaw exception: {ex.Message}");
            return $"OpenClaw 执行异常: {ex.Message}";
        }
    }

    private static string? FindOpenClaw()
    {
        string[] paths =
        [
            @"D:\npm-global\openclaw.cmd",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "openclaw.cmd"),
        ];

        foreach (var p in paths)
            if (File.Exists(p)) return p;

        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(';'))
        {
            var candidate = Path.Combine(dir.Trim(), "openclaw.cmd");
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }
}
