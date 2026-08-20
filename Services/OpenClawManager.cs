using System.Diagnostics;
using System.IO;

namespace MyAiAssistant.Services;

public class OpenClawManager : IOpenClawManager
{
    private Process? _process;
    private readonly string _exePath;

    public bool IsRunning => _process is { HasExited: false };

    public OpenClawManager()
    {
        _exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpenClaw", "OpenClaw.exe");
    }

    public void Start()
    {
        if (IsRunning || !File.Exists(_exePath)) return;
        try
        {
            _process = Process.Start(new ProcessStartInfo
            {
                FileName = _exePath,
                Arguments = "--port 8787",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                WorkingDirectory = Path.GetDirectoryName(_exePath)
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
}
