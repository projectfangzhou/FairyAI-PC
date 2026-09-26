using System.Text.Json.Nodes;

namespace MyAiAssistant.Services;

/// <summary>
/// Computer Use tools: let the LLM see the screen (via vision) and operate mouse/keyboard.
/// </summary>
public class TakeScreenshotTool : ITool
{
    private readonly IVisionService _vision;
    public TakeScreenshotTool(IVisionService vision) => _vision = vision;
    public string Name => "take_screenshot";
    public string Description => "Capture the current screen and describe its UI. Include clickable elements with approximate pixel coordinates (x, y) so they can be clicked.";
    public List<ToolParameter> Parameters => new()
    {
        new() { Name = "question", Description = "Optional: what to look for on screen. Leave empty for a full UI map.", Required = false, Type = "string" }
    };
    public async Task<string> ExecuteAsync(JsonObject args)
    {
        var q = args["question"]?.GetValue<string>() ?? "";
        var prompt = string.IsNullOrWhiteSpace(q)
            ? "请详细描述屏幕上的UI元素，包括按钮、输入框、菜单等的文字标签，以及每个可点击元素的大致像素坐标(x,y)。"
            : $"用户想在屏幕上找到/了解: {q}。请描述相关UI元素及其大致像素坐标(x,y)。";
        return await _vision.AnalyzeScreenAsync(prompt);
    }
}

public class MouseClickTool : ITool
{
    private readonly IScreenControlService _screen;
    public MouseClickTool(IScreenControlService screen) => _screen = screen;
    public string Name => "mouse_click";
    public string Description => "Left-click at screen coordinates (x, y)";
    public List<ToolParameter> Parameters => new()
    {
        new() { Name = "x", Type = "number", Description = "X coordinate in pixels" },
        new() { Name = "y", Type = "number", Description = "Y coordinate in pixels" }
    };
    public Task<string> ExecuteAsync(JsonObject args)
    {
        var x = (int)(args["x"]?.GetValue<double>() ?? 0);
        var y = (int)(args["y"]?.GetValue<double>() ?? 0);
        _screen.Click(x, y);
        return Task.FromResult($"Clicked at ({x}, {y})");
    }
}

public class MouseDoubleClickTool : ITool
{
    private readonly IScreenControlService _screen;
    public MouseDoubleClickTool(IScreenControlService screen) => _screen = screen;
    public string Name => "mouse_double_click";
    public string Description => "Double-click at screen coordinates (x, y)";
    public List<ToolParameter> Parameters => new()
    {
        new() { Name = "x", Type = "number", Description = "X coordinate in pixels" },
        new() { Name = "y", Type = "number", Description = "Y coordinate in pixels" }
    };
    public Task<string> ExecuteAsync(JsonObject args)
    {
        var x = (int)(args["x"]?.GetValue<double>() ?? 0);
        var y = (int)(args["y"]?.GetValue<double>() ?? 0);
        _screen.DoubleClick(x, y);
        return Task.FromResult($"Double-clicked at ({x}, {y})");
    }
}

public class MouseRightClickTool : ITool
{
    private readonly IScreenControlService _screen;
    public MouseRightClickTool(IScreenControlService screen) => _screen = screen;
    public string Name => "mouse_right_click";
    public string Description => "Right-click at screen coordinates (x, y)";
    public List<ToolParameter> Parameters => new()
    {
        new() { Name = "x", Type = "number", Description = "X coordinate in pixels" },
        new() { Name = "y", Type = "number", Description = "Y coordinate in pixels" }
    };
    public Task<string> ExecuteAsync(JsonObject args)
    {
        var x = (int)(args["x"]?.GetValue<double>() ?? 0);
        var y = (int)(args["y"]?.GetValue<double>() ?? 0);
        _screen.RightClick(x, y);
        return Task.FromResult($"Right-clicked at ({x}, {y})");
    }
}

public class TypeTextTool : ITool
{
    private readonly IScreenControlService _screen;
    public TypeTextTool(IScreenControlService screen) => _screen = screen;
    public string Name => "type_text";
    public string Description => "Type text into the currently focused input field (supports Chinese)";
    public List<ToolParameter> Parameters => new()
    {
        new() { Name = "text", Description = "Text to type" }
    };
    public Task<string> ExecuteAsync(JsonObject args)
    {
        var text = args["text"]?.GetValue<string>() ?? "";
        _screen.TypeText(text);
        return Task.FromResult($"Typed {text.Length} characters");
    }
}

public class PressKeyTool : ITool
{
    private readonly IScreenControlService _screen;
    public PressKeyTool(IScreenControlService screen) => _screen = screen;
    public string Name => "press_key";
    public string Description => "Press a key or shortcut. Keys: Enter, Tab, Escape, Space, Backspace, Delete, Home, End, PageUp, PageDown, Up, Down, Left, Right, F1-F5, Ctrl+C, Ctrl+V, Ctrl+X, Ctrl+Z, Ctrl+A, Ctrl+S";
    public List<ToolParameter> Parameters => new()
    {
        new() { Name = "key", Description = "Key name, e.g. Enter, Ctrl+C" }
    };
    public Task<string> ExecuteAsync(JsonObject args)
    {
        var key = args["key"]?.GetValue<string>() ?? "";
        _screen.PressKey(key);
        return Task.FromResult($"Pressed {key}");
    }
}

public class ScrollTool : ITool
{
    private readonly IScreenControlService _screen;
    public ScrollTool(IScreenControlService screen) => _screen = screen;
    public string Name => "scroll";
    public string Description => "Scroll the mouse wheel. Positive delta scrolls up, negative scrolls down.";
    public List<ToolParameter> Parameters => new()
    {
        new() { Name = "delta", Type = "number", Description = "Scroll amount, e.g. -3 (down) or 3 (up)" }
    };
    public Task<string> ExecuteAsync(JsonObject args)
    {
        var delta = (int)(args["delta"]?.GetValue<double>() ?? -3);
        _screen.Scroll(delta);
        return Task.FromResult(delta < 0 ? "Scrolled down" : "Scrolled up");
    }
}
