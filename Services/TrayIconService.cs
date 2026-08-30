using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MyAiAssistant.Windows;

namespace MyAiAssistant.Services;

/// <summary>
/// System tray icon with context menu. Manages show/hide windows and quick actions.
/// </summary>
public class TrayIconService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private readonly FloatingOrbWindow _orb;
    private readonly DynamicIslandWindow _island;
    private readonly Func<bool, bool> _setAutoStart;
    private readonly Action _showClipboardHistory;
    private readonly Action _showOllamaSetup;
    private readonly string _logPath;

    public event Action? ExitRequested;

    public TrayIconService(
        FloatingOrbWindow orb,
        DynamicIslandWindow island,
        Func<bool, bool> setAutoStart,
        Action showClipboardHistory,
        Action showOllamaSetup)
    {
        _orb = orb;
        _island = island;
        _setAutoStart = setAutoStart;
        _showClipboardHistory = showClipboardHistory;
        _showOllamaSetup = showOllamaSetup;
        _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
    }

    public void Show()
    {
        var icon = LoadIcon();
        _notifyIcon = new NotifyIcon
        {
            Text = "Fairy AI 语音助手",
            Icon = icon,
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };

        _notifyIcon.DoubleClick += (_, _) => ToggleVisibility();
        Log("Tray icon shown");
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        var showHide = new ToolStripMenuItem("显示/隐藏");
        showHide.Click += (_, _) => ToggleVisibility();
        menu.Items.Add(showHide);

        var autoStart = new ToolStripMenuItem("开机自启");
        autoStart.Checked = AutoStartService.IsEnabled();
        autoStart.Click += (_, e) =>
        {
            var newState = !autoStart.Checked;
            autoStart.Checked = _setAutoStart(newState);
        };
        menu.Items.Add(autoStart);

        var clipboard = new ToolStripMenuItem("剪贴板历史");
        clipboard.Click += (_, _) => _showClipboardHistory();
        menu.Items.Add(clipboard);

        var ollama = new ToolStripMenuItem("本地模型部署");
        ollama.Click += (_, _) => _showOllamaSetup();
        menu.Items.Add(ollama);

        menu.Items.Add(new ToolStripSeparator());

        var exit = new ToolStripMenuItem("退出 Fairy AI");
        exit.Click += (_, _) =>
        {
            _notifyIcon?.Dispose();
            ExitRequested?.Invoke();
        };
        menu.Items.Add(exit);

        return menu;
    }

    private void ToggleVisibility()
    {
        if (_orb.IsVisible || _island.IsVisible)
        {
            _orb.Hide();
            _island.Hide();
            Log("Windows hidden to tray");
        }
        else
        {
            _orb.Show();
            Log("Windows shown from tray");
        }
    }

    /// <summary>Show a tray balloon notification.</summary>
    public void Notify(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        try
        {
            _notifyIcon?.ShowBalloonTip(3000, title, message, icon);
        }
        catch { }
    }

    private static System.Drawing.Icon LoadIcon()
    {
        // Draw a simple purple circle icon at runtime (no external asset needed)
        using var bmp = new System.Drawing.Bitmap(32, 32);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(123, 104, 238)); // #7b68ee
            g.FillEllipse(brush, 4, 4, 24, 24);
        }
        var hIcon = bmp.GetHicon();
        return System.Drawing.Icon.FromHandle(hIcon);
    }

    private void Log(string msg)
    {
        try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] [Tray] {msg}\n"); } catch { }
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
        _notifyIcon = null;
    }
}
