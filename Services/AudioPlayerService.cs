using System.IO;
using System.Media;

namespace MyAiAssistant.Services;

public class AudioPlayerService : IAudioPlayerService
{
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
    private SoundPlayer? _player;

    public Task PlayAsync(byte[] audioData)
    {
        if (audioData.Length == 0) return Task.CompletedTask;

        try
        {
            // Stop any currently playing audio first
            Stop();

            // Save to temp WAV file and play
            var tempFile = Path.Combine(Path.GetTempPath(), $"fairy_tts_play_{Guid.NewGuid():N}.wav");
            File.WriteAllBytes(tempFile, audioData);

            _player = new SoundPlayer(tempFile);
            _player.Play();

            Log($"TTS: playing audio ({audioData.Length} bytes)");
        }
        catch (Exception ex)
        {
            Log($"TTS play error: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public void Stop()
    {
        try { _player?.Stop(); } catch { }
        _player?.Dispose();
        _player = null;
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }
}
