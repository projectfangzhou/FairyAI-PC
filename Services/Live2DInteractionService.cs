using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Point = System.Windows.Point;

namespace MyAiAssistant.Services;

/// <summary>
/// Live2D interaction manager: draggable model, clickable body parts
/// (head, hands, etc.) that send reactions to cloud AI and animate the model.
/// </summary>
public class Live2DInteractionService
{
    private Window? _live2dWindow;
    private Point _dragStart;
    private bool _isDragging;
    private static readonly string LogPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    /// <summary>Make the Live2D window draggable.</summary>
    public void MakeDraggable(Window window)
    {
        _live2dWindow = window;
        window.MouseLeftButtonDown += OnMouseDown;
        window.MouseMove += OnMouseMove;
        window.MouseLeftButtonUp += OnMouseUp;
        Log("Live2D window is now draggable");
    }

    /// <summary>Handle click on a Live2D model body part (head, hand, body, etc).</summary>
    public async Task<string> HandlePartClickAsync(string partName, Point position)
    {
        Log($"Live2D part clicked: {partName} at ({position.X}, {position.Y})");

        var config = ConfigManager.Load();
        var prompt = $"用户点击了Live2D模型的{partName}部位。请给出一个可爱的反应（动作描述+表情+简短台词）。角色是: {config.Personality.Name}";

        try
        {
            var llm = new LlmService();
            var reaction = new System.Text.StringBuilder();
            await foreach (var chunk in llm.StreamChatAsync(
                [], prompt, config.LLM.BaseUrl, config.LLM.Model, config.LLM.ApiKey))
            {
                reaction.Append(chunk);
            }

            var result = reaction.ToString().Trim();
            Log($"Live2D reaction: {result[..Math.Min(80, result.Length)]}...");
            return result;
        }
        catch (Exception ex)
        {
            Log($"Live2D reaction error: {ex.Message}");
            return $"[点击了{partName}]";
        }
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_live2dWindow == null) return;
        _isDragging = true;
        _dragStart = e.GetPosition(_live2dWindow);
        _live2dWindow.CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _live2dWindow == null) return;
        var current = e.GetPosition(_live2dWindow);
        _live2dWindow.Left += current.X - _dragStart.X;
        _live2dWindow.Top += current.Y - _dragStart.Y;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        _live2dWindow?.ReleaseMouseCapture();
    }

    private static void Log(string msg)
    {
        try { System.IO.File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [L2D] {msg}\n"); } catch { }
    }
}
