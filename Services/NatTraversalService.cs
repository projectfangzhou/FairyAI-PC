using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace MyAiAssistant.Services;

/// <summary>
/// NAT traversal service using ngrok to expose local services to the internet.
/// Creates a tunnel from a public URL to the local LAN server.
/// </summary>
public class NatTraversalService : IDisposable
{
    private Process? _ngrokProcess;
    private CancellationTokenSource? _cts;
    private readonly string _logPath;
    private readonly NatTraversalConfig _config;
    private string? _publicUrl;

    public bool IsRunning => _ngrokProcess != null && !_ngrokProcess.HasExited;
    public string? PublicUrl => _publicUrl;

    public event EventHandler<string>? TunnelEstablished;
    public event EventHandler<string>? TunnelLost;

    public NatTraversalService()
    {
        _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
        _config = ConfigManager.Load().NatTraversal;
    }

    public async Task StartAsync(int localPort)
    {
        if (IsRunning) return;

        if (_config.Provider != "ngrok" || string.IsNullOrWhiteSpace(_config.AuthToken))
        {
            Log("NAT traversal not configured or unsupported provider");
            return;
        }

        try
        {
            // Check if ngrok is installed
            var ngrokPath = FindNgrok();
            if (ngrokPath == null)
            {
                Log("ngrok not found. Download from https://ngrok.com/download");
                return;
            }

            // Authenticate
            var authResult = await RunNgrokCommand(ngrokPath, $"authtoken {_config.AuthToken}");
            if (!authResult)
            {
                Log("ngrok authtoken authentication failed");
                return;
            }

            // Start tunnel
            _cts = new CancellationTokenSource();
            var region = _config.Region ?? "ap";

            _ngrokProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ngrokPath,
                    Arguments = $"http {localPort} --region={region} --log=stdout",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            _ngrokProcess.Start();
            _ = ReadNgrokOutputAsync(_ngrokProcess, _cts.Token);

            // Wait for tunnel to establish
            await Task.Delay(3000, _cts.Token);

            // Query the ngrok API for the public URL
            _publicUrl = await GetNgrokPublicUrlAsync();
            if (_publicUrl != null)
            {
                Log($"NAT tunnel established: {_publicUrl}");
                TunnelEstablished?.Invoke(this, _publicUrl);

                // Save to config
                var config = ConfigManager.Load();
                config.NatTraversal.PublicUrl = _publicUrl;
                ConfigManager.Save(config);
            }
        }
        catch (Exception ex)
        {
            Log($"NAT traversal start error: {ex.Message}");
        }
    }

    public void Stop()
    {
        _cts?.Cancel();

        if (_ngrokProcess != null && !_ngrokProcess.HasExited)
        {
            try { _ngrokProcess.Kill(); } catch { }
        }
        _ngrokProcess = null;
        _publicUrl = null;

        Log("NAT tunnel closed");
    }

    private async Task ReadNgrokOutputAsync(Process process, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && !process.HasExited)
            {
                var line = await process.StandardOutput.ReadLineAsync();
                if (line == null) break;
                Log($"ngrok: {line}");
            }
        }
        catch { }
    }

    private async Task<string?> GetNgrokPublicUrlAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetStringAsync("http://127.0.0.1:4040/api/tunnels");
            var doc = JsonDocument.Parse(response);

            foreach (var tunnel in doc.RootElement.GetProperty("tunnels").EnumerateArray())
            {
                var publicUrl = tunnel.GetProperty("public_url").GetString();
                if (publicUrl != null && publicUrl.StartsWith("https"))
                    return publicUrl;
            }
        }
        catch { }
        return null;
    }

    private static string? FindNgrok()
    {
        // Check common locations
        var candidates = new[]
        {
            "ngrok.exe",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ngrok.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ngrok.exe"),
            @"C:\tools\ngrok.exe",
            @"C:\ProgramData\ngrok.exe"
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path)) return path;
        }

        // Try PATH
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "ngrok",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            var proc = Process.Start(psi);
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit();
                if (proc.ExitCode == 0 && !string.IsNullOrEmpty(output))
                    return output.Split('\n')[0].Trim();
            }
        }
        catch { }

        return null;
    }

    private static async Task<bool> RunNgrokCommand(string ngrokPath, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ngrokPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            var proc = Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync();
                return proc.ExitCode == 0;
            }
        }
        catch { }
        return false;
    }

    public void Dispose()
    {
        Stop();
    }

    private void Log(string msg)
    {
        try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] [NAT] {msg}\n"); } catch { }
    }
}
