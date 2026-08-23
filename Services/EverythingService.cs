using System.Diagnostics;
using System.IO;

namespace MyAiAssistant.Services;

public class EverythingService : IEverythingService
{
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
    private static readonly string EsExePath = @"D:\Dev\Everything\SDK\es.exe";

    public bool IsAvailable => File.Exists(EsExePath);

    public List<string> SearchFiles(string query, int maxResults = 10)
    {
        var results = new List<string>();
        if (!IsAvailable || string.IsNullOrWhiteSpace(query)) return results;

        try
        {
            Log($"Everything: searching '{query}'");

            var psi = new ProcessStartInfo
            {
                FileName = EsExePath,
                Arguments = $"\"{query}\" -max-results {maxResults}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(EsExePath) ?? ""
            };

            using var process = Process.Start(psi);
            if (process == null) { Log("Everything: process is null"); return results; }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            Log($"Everything: exit={process.ExitCode}, output_len={output.Length}");

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    results.Add(trimmed);
                    Log($"Everything: found '{trimmed}'");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Everything error: {ex.Message}");
        }

        return results;
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }
}
