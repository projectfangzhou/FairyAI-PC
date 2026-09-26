using System.Diagnostics;
using System.IO;

namespace MyAiAssistant.Services;

/// <summary>
/// Slices a reference audio file into small chunks via ffmpeg for GPT-SoVITS
/// voice cloning quality improvement.
/// </summary>
public class AudioSlicer
{
    private readonly string _ffmpegPath;
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public AudioSlicer()
    {
        var config = ConfigManager.Load();
        _ffmpegPath = config.TTS.FfmpegPath;
        if (string.IsNullOrWhiteSpace(_ffmpegPath) || !File.Exists(_ffmpegPath))
            _ffmpegPath = "ffmpeg.exe";
    }

    /// <summary>Slice audio into chunks of specified duration (seconds). Returns list of file paths.</summary>
    public async Task<List<string>> SliceAsync(string inputPath, double chunkSeconds = 10.0, string outputDir = "")
    {
        var results = new List<string>();
        if (!File.Exists(inputPath)) return results;

        if (string.IsNullOrWhiteSpace(outputDir))
            outputDir = Path.Combine(Path.GetTempPath(), $"fairy_slices_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        var outPattern = Path.Combine(outputDir, $"{baseName}_slice_%03d.wav");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = $"-y -i \"{inputPath}\" -f segment -segment_time {chunkSeconds:F1} -ar 24000 -ac 1 \"{outPattern}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
            };

            using var process = Process.Start(psi);
            if (process == null) return results;
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                results = Directory.GetFiles(outputDir, $"{baseName}_slice_*.wav")
                    .OrderBy(f => f).ToList();
                Log($"Sliced '{Path.GetFileName(inputPath)}' into {results.Count} chunks");
            }
        }
        catch (Exception ex)
        {
            Log($"Slice error: {ex.Message}");
        }

        return results;
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [SLICE] {msg}\n"); } catch { }
    }
}
