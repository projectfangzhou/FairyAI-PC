using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MyAiAssistant.Services;

/// <summary>
/// Manages Live2D vs Dynamic Island display logic:
/// - No Live2D: show Dynamic Island normally
/// - With Live2D: show only orb; on click show Live2D in bottom-right corner
/// </summary>
public class Live2DLayoutManager
{
    private Window? _live2dWindow;
    private bool _isLive2dVisible;
    private static readonly string LogPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    /// <summary>Setup layout based on Live2D configuration.</summary>
    public void Configure(bool hasLive2D)
    {
        if (hasLive2D)
        {
            Log("Layout: Live2D mode — orb only, Live2D on click");
        }
        else
        {
            Log("Layout: Dynamic Island mode");
        }
    }

    /// <summary>Show Live2D in bottom-right corner.</summary>
    public void ShowLive2DBottomRight(Window live2dWindow)
    {
        _live2dWindow = live2dWindow;
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        live2dWindow.Left = screenWidth - live2dWindow.Width - 20;
        live2dWindow.Top = screenHeight - live2dWindow.Height - 20;
        live2dWindow.Show();
        _isLive2dVisible = true;
        Log("Live2D shown in bottom-right");
    }

    /// <summary>Hide Live2D.</summary>
    public void HideLive2D()
    {
        _live2dWindow?.Hide();
        _isLive2dVisible = false;
    }

    /// <summary>Toggle Live2D visibility.</summary>
    public void ToggleLive2D()
    {
        if (_isLive2dVisible) HideLive2D();
        else if (_live2dWindow != null) ShowLive2DBottomRight(_live2dWindow);
    }

    public bool IsLive2DVisible => _isLive2dVisible;

    private static void Log(string msg)
    {
        try { System.IO.File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [LAYOUT] {msg}\n"); } catch { }
    }
}
