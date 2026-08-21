using System.Diagnostics;
using System.IO;

namespace MyAiAssistant.Services;

public class OpenClawManager : IOpenClawManager
{
    private Process? _process;

    public bool IsRunning => _process is { HasExited: false };

    public void Start()
    {
        if (IsRunning) return;

        // OpenClaw is optional — skip silently if not installed
        var exePath = FindOpenClaw();
        if (exePath == null) return;

        try
        {
            _process = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "gateway run --port 8787",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OpenClaw] Start failed: {ex.Message}");
        }
    }

    public void Stop()
    {
        try { _process?.Kill(true); } catch { }
        _process?.Dispose();
        _process = null;
    }

    public void Dispose() => Stop();

    private static string? FindOpenClaw()
    {
        // Check common locations
        string[] paths =
        [
            @"D:\npm-global\openclaw.cmd",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "openclaw.cmd"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpenClaw", "openclaw.cmd"),
        ];

        foreach (var p in paths)
        {
            if (File.Exists(p)) return p;
        }

        // Check PATH
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(';'))
        {
            var candidate = Path.Combine(dir.Trim(), "openclaw.cmd");
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }
}
