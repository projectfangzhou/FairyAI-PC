using System.Diagnostics;
using System.IO;

namespace MyAiAssistant.Services;

public class OpenClawManager : IOpenClawManager
{
    private Process? _process;
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public bool IsRunning => _process is { HasExited: false };

    public void Start()
    {
        if (IsRunning) return;

        var exePath = FindOpenClaw();
        if (exePath == null)
        {
            Log("OpenClaw not found, skipping");
            return;
        }

        try
        {
            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "gateway run --allow-unconfigured --port 8787 --bind loopback",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? ""
                },
                EnableRaisingEvents = true
            };

            _process.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Log($"[OpenClaw] {e.Data}"); };
            _process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Log($"[OpenClaw ERR] {e.Data}"); };

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            Log($"OpenClaw gateway started (PID: {_process.Id})");
        }
        catch (Exception ex)
        {
            Log($"OpenClaw start failed: {ex.Message}");
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
