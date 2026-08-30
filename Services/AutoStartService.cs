using System.IO;
using Microsoft.Win32;

namespace MyAiAssistant.Services;

/// <summary>
/// Manages auto-start on Windows login via HKCU\...\Run registry key.
/// </summary>
public static class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "FairyAI";

    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    /// <summary>Check whether auto-start is currently enabled.</summary>
    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            var value = key?.GetValue(AppName) as string;
            return !string.IsNullOrEmpty(value);
        }
        catch { return false; }
    }

    /// <summary>Enable or disable auto-start. Returns the new state.</summary>
    public static bool SetEnabled(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key == null) return IsEnabled();

            var exePath = Environment.ProcessPath ?? "";

            if (enable)
            {
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return IsEnabled();
                key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }

            Log($"Auto-start {(enable ? "enabled" : "disabled")}: {exePath}");
            return IsEnabled();
        }
        catch (Exception ex)
        {
            Log($"Auto-start error: {ex.Message}");
            return IsEnabled();
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [AutoStart] {msg}\n"); } catch { }
    }
}
