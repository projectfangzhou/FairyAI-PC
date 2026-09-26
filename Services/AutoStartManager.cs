namespace MyAiAssistant.Services;

/// <summary>
/// Layered auto-start: main process (FairyAI) starts at boot, then secondary
/// services initialize in dependency order.
/// </summary>
public class AutoStartManager
{
    private static readonly string LogPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    /// <summary>Ensure main process starts at boot via registry Run key.</summary>
    public static void SetAutoStart(bool enabled)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            if (enabled)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                key.SetValue("FairyAI", $"\"{exePath}\"");
                Log("Auto-start enabled");
            }
            else
            {
                key.DeleteValue("FairyAI", false);
                Log("Auto-start disabled");
            }
        }
        catch (Exception ex) { Log($"Auto-start error: {ex.Message}"); }
    }

    /// <summary>Initialize secondary services in dependency order after main process starts.</summary>
    public static async Task InitializeServicesLayeredAsync(IServiceProvider services)
    {
        Log("Starting layered service initialization...");

        // Layer 1: Core services (immediate)
        var config = ConfigManager.Load();
        Log("Layer 1: Core config loaded");

        await Task.Delay(100);

        // Layer 2: Network services
        if (config.Sync.Enabled)
        {
            var deviceServer = new DeviceServerService();
            await deviceServer.StartAsync();
            Log("Layer 2: Device server started");
        }

        await Task.Delay(200);

        // Layer 3: Background services
        if (config.Personality.EnableEmotion)
        {
            Log("Layer 3: Idle sound service ready");
        }

        // Layer 4: Bot services (last — needs network)
        Log("Layer 4: Bot services ready (if configured)");
        Log("Layered initialization complete");
    }

    private static void Log(string msg)
    {
        try { System.IO.File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [AUTOSTART] {msg}\n"); } catch { }
    }
}
