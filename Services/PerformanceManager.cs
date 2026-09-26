using System.Diagnostics;
using System.IO;

namespace MyAiAssistant.Services;

/// <summary>
/// Performance mode manager — detects resource-heavy applications (games, video editors)
/// and disables non-essential features based on selected mode (high/balanced/low).
/// </summary>
public class PerformanceManager
{
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
    private readonly HashSet<string> _heavyProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Games
        "steam", "epicgameslauncher", "battle.net", "origin", "uplay",
        "eldenring", "cyberpunk2077", "starfield", "baldursgate3",
        "league of legends", "valorant", "overwatch", "dota2", "csgo",
        // Video/3D
        "premiere", "aftereffects", "blender", "davinciresolve", "maya",
        // VMs
        "vmware", "virtualbox", "qemu",
    };

    /// <summary>Check if any heavy application is running.</summary>
    public bool IsHeavyAppRunning()
    {
        try
        {
            var processes = Process.GetProcesses();
            foreach (var p in processes)
            {
                if (_heavyProcessNames.Contains(p.ProcessName))
                {
                    Log($"Heavy app detected: {p.ProcessName}");
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    /// <summary>Get which features should be disabled based on performance mode.</summary>
    public HashSet<string> GetDisabledFeatures()
    {
        var config = ConfigManager.Load();
        var mode = config.Performance.Mode;
        var disabled = new HashSet<string>();

        if (!config.Performance.AutoDetectGames) return disabled;

        var isHeavy = IsHeavyAppRunning();

        switch (mode)
        {
            case "low":
                disabled.Add("Live2D");
                disabled.Add("IdleSounds");
                disabled.Add("ScreenAnalysis");
                disabled.Add("LocalVision");
                disabled.Add("Voiceprint");
                if (isHeavy) disabled.Add("TTS");
                break;
            case "balanced":
                if (isHeavy)
                {
                    disabled.Add("Live2D");
                    disabled.Add("IdleSounds");
                    disabled.Add("LocalVision");
                }
                break;
            case "high":
                // Keep everything on
                break;
        }

        if (disabled.Count > 0)
            Log($"Performance mode '{mode}': disabled [{string.Join(", ", disabled)}]");
        return disabled;
    }

    /// <summary>Check if a specific feature is enabled.</summary>
    public bool IsFeatureEnabled(string feature)
    {
        return !GetDisabledFeatures().Contains(feature);
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [PERF] {msg}\n"); } catch { }
    }
}
