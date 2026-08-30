using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace MyAiAssistant.Services;

public class OllamaModel
{
    public string Name { get; set; } = "";
    public string Size { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>
/// One-click local model deployment via Ollama: detects installation,
/// offers download, pulls models, and configures the LLM to use it.
/// </summary>
public class OllamaService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public static readonly List<OllamaModel> AvailableModels = new()
    {
        new OllamaModel { Name = "qwen2.5:7b", Size = "~4.7GB", Description = "通义千问 7B，中文能力强，推荐" },
        new OllamaModel { Name = "qwen2.5:3b", Size = "~1.9GB", Description = "通义千问 3B，轻量快速" },
        new OllamaModel { Name = "llama3.2:3b", Size = "~2.0GB", Description = "Meta Llama 3.2，通用对话" },
        new OllamaModel { Name = "llama3.2:1b", Size = "~1.3GB", Description = "Meta Llama 3.2 最小版，极速" },
        new OllamaModel { Name = "deepseek-r1:7b", Size = "~4.7GB", Description = "DeepSeek R1 推理模型" },
        new OllamaModel { Name = "gemma2:2b", Size = "~1.6GB", Description = "Google Gemma 2 轻量版" },
    };

    /// <summary>Whether the Ollama CLI is available on PATH.</summary>
    public static bool IsOllamaInstalled()
    {
        try
        {
            var psi = new ProcessStartInfo("ollama", "--version")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
            return proc?.ExitCode == 0;
        }
        catch { return false; }
    }

    /// <summary>Open the Ollama download page in the default browser.</summary>
    public static void OpenOllamaDownload()
    {
        try { Process.Start(new ProcessStartInfo("https://ollama.com/download") { UseShellExecute = true }); } catch { }
    }

    /// <summary>List models already pulled locally.</summary>
    public static async Task<List<string>> ListLocalModelsAsync()
    {
        var result = new List<string>();
        try
        {
            var output = await RunOllamaAsync("list");
            foreach (var line in output.Split('\n').Skip(1))
            {
                var name = line.Trim().Split(' ')[0];
                if (!string.IsNullOrEmpty(name) && name != "NAME")
                    result.Add(name);
            }
        }
        catch (Exception ex) { Log($"list error: {ex.Message}"); }
        return result;
    }

    /// <summary>Pull a model. Progress is reported as lines of ollama output.</summary>
    public static async Task PullModelAsync(string modelName, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        Log($"Pulling model: {modelName}");
        var psi = new ProcessStartInfo("ollama", $"pull {modelName}")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc == null) throw new InvalidOperationException("无法启动 ollama");

        var tcs = new TaskCompletionSource<bool>();
        proc.EnableRaisingEvents = true;
        proc.Exited += (_, _) => tcs.TrySetResult(proc.ExitCode == 0);
        proc.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) progress?.Report(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) progress?.Report(e.Data); };

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await tcs.Task.WaitAsync(ct);
        if (!tcs.Task.Result) throw new InvalidOperationException("模型下载失败");
        Log($"Model pulled: {modelName}");
    }

    /// <summary>Check the Ollama service is running (API reachable).</summary>
    public static async Task<bool> IsOllamaRunningAsync()
    {
        try
        {
            var resp = await Http.GetAsync("http://localhost:11434/api/tags");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    /// <summary>Configure the app to use a local Ollama model.</summary>
    public static void ConfigureLocalModel(string modelName)
    {
        var config = ConfigManager.Load();
        config.LLM.Provider = "Ollama";
        config.LLM.ApiKey = "ollama"; // local, no key needed
        config.LLM.BaseUrl = "http://localhost:11434/v1/chat/completions";
        config.LLM.Model = modelName;
        ConfigManager.Save(config);
        Log($"Configured local model: {modelName}");
    }

    private static async Task<string> RunOllamaAsync(string args)
    {
        var psi = new ProcessStartInfo("ollama", args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        if (proc == null) return "";
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return stdout;
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [Ollama] {msg}\n"); } catch { }
    }
}
