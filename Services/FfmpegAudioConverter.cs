using System.Diagnostics;
using System.IO;

namespace MyAiAssistant.Services;

public interface IAudioConverter
{
    bool IsAvailable { get; }
    Task<byte[]> ConvertToWavAsync(byte[] audioData);
    Task<byte[]> ConvertAsync(byte[] audioData, string outputFormat, int sampleRate = 24000);
    Task<byte[]> TrimAsync(byte[] audioData, TimeSpan start, TimeSpan end);
}

/// <summary>
/// Audio format conversion via ffmpeg. Used to normalize audio from different
/// TTS providers (MP3/OGG/etc) into WAV for the SoundPlayer pipeline.
/// </summary>
public class FfmpegAudioConverter : IAudioConverter
{
    private readonly string _ffmpegPath;
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public bool IsAvailable => File.Exists(_ffmpegPath);

    public FfmpegAudioConverter()
    {
        var config = ConfigManager.Load();
        _ffmpegPath = config.TTS.FfmpegPath;

        // Auto-detect common install paths if not configured
        if (string.IsNullOrWhiteSpace(_ffmpegPath) || !File.Exists(_ffmpegPath))
        {
            var candidates = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin", "ffmpeg.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "ffmpeg", "bin", "ffmpeg.exe"),
                "ffmpeg.exe" // PATH
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c)) { _ffmpegPath = c; break; }
            }
        }
    }

    public async Task<byte[]> ConvertToWavAsync(byte[] audioData)
        => await ConvertAsync(audioData, "wav", 24000);

    public async Task<byte[]> ConvertAsync(byte[] audioData, string outputFormat, int sampleRate = 24000)
    {
        if (!IsAvailable)
        {
            Log("ffmpeg not available, returning original audio");
            return audioData;
        }

        var inputPath = Path.Combine(Path.GetTempPath(), $"fairy_ffmpeg_in_{Guid.NewGuid():N}");
        var outputPath = Path.Combine(Path.GetTempPath(), $"fairy_ffmpeg_out_{Guid.NewGuid():N}.{outputFormat}");

        try
        {
            await File.WriteAllBytesAsync(inputPath, audioData);

            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = $"-y -i \"{inputPath}\" -ar {sampleRate} -ac 1 -f {outputFormat} \"{outputPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            using var process = Process.Start(psi);
            if (process == null) return audioData;
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && File.Exists(outputPath))
            {
                var result = await File.ReadAllBytesAsync(outputPath);
                Log($"ffmpeg: converted {audioData.Length} bytes -> {result.Length} bytes ({outputFormat})");
                return result;
            }

            var stderr = await process.StandardError.ReadToEndAsync();
            Log($"ffmpeg error (exit {process.ExitCode}): {stderr[..Math.Min(200, stderr.Length)]}");
            return audioData;
        }
        catch (Exception ex)
        {
            Log($"ffmpeg exception: {ex.Message}");
            return audioData;
        }
        finally
        {
            try { File.Delete(inputPath); } catch { }
            try { File.Delete(outputPath); } catch { }
        }
    }

    public async Task<byte[]> TrimAsync(byte[] audioData, TimeSpan start, TimeSpan end)
    {
        if (!IsAvailable) return audioData;

        var inputPath = Path.Combine(Path.GetTempPath(), $"fairy_ffmpeg_trim_in_{Guid.NewGuid():N}.wav");
        var outputPath = Path.Combine(Path.GetTempPath(), $"fairy_ffmpeg_trim_out_{Guid.NewGuid():N}.wav");

        try
        {
            await File.WriteAllBytesAsync(inputPath, audioData);
            var duration = (end - start).TotalSeconds;

            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = $"-y -i \"{inputPath}\" -ss {start.TotalSeconds:F3} -t {duration:F3} -ar 24000 -ac 1 \"{outputPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
            };

            using var process = Process.Start(psi);
            if (process == null) return audioData;
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && File.Exists(outputPath))
                return await File.ReadAllBytesAsync(outputPath);

            return audioData;
        }
        finally
        {
            try { File.Delete(inputPath); } catch { }
            try { File.Delete(outputPath); } catch { }
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [FFMPEG] {msg}\n"); } catch { }
    }
}
