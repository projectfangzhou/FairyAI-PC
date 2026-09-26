using System.Diagnostics;
using System.IO;

namespace MyAiAssistant.Services;

/// <summary>
/// Video download via yt-dlp. Supports YouTube, Bilibili, and other sites.
/// </summary>
public class YtDlpService
{
    private readonly string _ytDlpPath;
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_ytDlpPath) && (File.Exists(_ytDlpPath) || _ytDlpPath == "yt-dlp.exe");

    public YtDlpService()
    {
        // Try common paths
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "yt-dlp", "yt-dlp.exe"),
            "yt-dlp.exe"
        };
        _ytDlpPath = candidates.FirstOrDefault(c => File.Exists(c) || c == "yt-dlp.exe") ?? "yt-dlp.exe";
    }

    /// <summary>Download video to specified output directory. Returns file path.</summary>
    public async Task<string> DownloadVideoAsync(string url, string outputDir, string format = "best")
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        Directory.CreateDirectory(outputDir);

        var outTemplate = Path.Combine(outputDir, "%(title)s.%(ext)s");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ytDlpPath,
                Arguments = $"-f {format} -o \"{outTemplate}\" \"{url}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(psi);
            if (process == null) return "";
            var stdout = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                // Find the downloaded file (most recent in outputDir)
                var file = Directory.GetFiles(outputDir)
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .FirstOrDefault();
                Log($"Downloaded: {file}");
                return file ?? "";
            }
            var stderr = await process.StandardError.ReadToEndAsync();
            Log($"yt-dlp error: {stderr[..Math.Min(200, stderr.Length)]}");
            return "";
        }
        catch (Exception ex)
        {
            Log($"yt-dlp exception: {ex.Message}");
            return "";
        }
    }

    /// <summary>Download audio only (for BiliLearn knowledge extraction).</summary>
    public async Task<string> DownloadAudioAsync(string url, string outputDir)
    {
        return await DownloadVideoAsync(url, outputDir, "bestaudio");
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [YTDLP] {msg}\n"); } catch { }
    }
}
