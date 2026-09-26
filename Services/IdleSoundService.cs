using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace MyAiAssistant.Services;

/// <summary>
/// Idle sound player — plays personality-based idle/standby sounds when Live2D
/// is in persistent mode and ASR is not active. If personality is a game character,
/// searches the web for original game idle sounds.
/// </summary>
public class IdleSoundService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
    private readonly Random _rng = new();
    private DateTime _lastPlayed = DateTime.MinValue;
    private readonly System.Windows.Threading.DispatcherTimer _timer;

    public event Action<byte[]>? SoundReady;

    public IdleSoundService()
    {
        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_rng.Next(120, 300)) // random 2-5 min
        };
        _timer.Tick += async (_, _) => await PlayIdleSoundAsync();
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private async Task PlayIdleSoundAsync()
    {
        // Randomize next interval
        _timer.Interval = TimeSpan.FromSeconds(_rng.Next(120, 300));

        // Don't play too frequently
        if ((DateTime.Now - _lastPlayed).TotalSeconds < 60) return;

        var config = ConfigManager.Load();
        if (!config.Personality.EnableEmotion) return;

        try
        {
            string soundText;
            if (config.Personality.IsGameCharacter && !string.IsNullOrWhiteSpace(config.Personality.GameCharacterName))
            {
                soundText = await SearchGameIdleSoundAsync(config.Personality.GameCharacterName);
            }
            else
            {
                soundText = GeneratePersonalityIdleText(config.Personality);
            }

            if (string.IsNullOrWhiteSpace(soundText)) return;

            // Synthesize via TTS
            var tts = new MiMoTtsService();
            var audio = await tts.SynthesizeAsync(soundText);
            if (audio.Length > 0)
            {
                _lastPlayed = DateTime.Now;
                SoundReady?.Invoke(audio);
                Log($"Idle sound played: '{soundText[..Math.Min(40, soundText.Length)]}'");
            }
        }
        catch (Exception ex)
        {
            Log($"Idle sound error: {ex.Message}");
        }
    }

    /// <summary>Search web for original game character idle voice lines.</summary>
    private async Task<string> SearchGameIdleSoundAsync(string characterName)
    {
        try
        {
            var search = new TavilySearchService();
            var result = await search.SearchAsync($"{characterName} 待机语音 台词 原神");
            // Extract a short idle line
            var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(l => l.Length > 2 && l.Length < 50)
                .ToList();
            return lines.Count > 0 ? lines[_rng.Next(lines.Count)] : $"{characterName}伸了个懒腰，准备好了吗？";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>Generate personality-appropriate idle text.</summary>
    private static string GeneratePersonalityIdleText(PersonalityConfig personality)
    {
        var prompts = personality.SystemPrompt.ToLowerInvariant();
        if (prompts.Contains("活泼") || prompts.Contains("可爱") || prompts.Contains("派蒙"))
            return "哼哼，我在呢！有什么需要帮忙的吗？";
        if (prompts.Contains("温柔") || prompts.Contains("温暖"))
            return "我在哦，需要我的时候随时说一声。";
        if (prompts.Contains("严肃") || prompts.Contains("专业"))
            return "系统就绪。请下达指令。";
        return "嗯...我在呢。";
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [IDLE] {msg}\n"); } catch { }
    }
}
