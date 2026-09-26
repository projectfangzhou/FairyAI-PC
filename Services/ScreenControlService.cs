using System.Runtime.InteropServices;

namespace MyAiAssistant.Services;

public interface IScreenControlService
{
    void MoveTo(int x, int y);
    void Click(int x, int y);
    void DoubleClick(int x, int y);
    void RightClick(int x, int y);
    void Scroll(int delta);
    void TypeText(string text);
    void PressKey(string key);
    (int Width, int Height) GetScreenSize();
}

/// <summary>
/// Mouse/keyboard automation via user32 SendInput / SetCursorPos.
/// Used by screen automation (Computer Use) tools so the LLM can operate the desktop.
/// </summary>
public class ScreenControlService : IScreenControlService
{
    private const int InputMouse = 0;
    private const int InputKeyboard = 1;
    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventWheel = 0x0800;
    private const uint KeyboardKeyup = 0x0002;
    private const uint KeyboardUnicode = 0x0004;
    private const uint KeyboardScancode = 0x0008;

    public void MoveTo(int x, int y) => SetCursorPos(x, y);

    public void Click(int x, int y)
    {
        SetCursorPos(x, y);
        Thread.Sleep(30);
        mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(20);
        mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
    }

    public void DoubleClick(int x, int y)
    {
        Click(x, y);
        Thread.Sleep(50);
        Click(x, y);
    }

    public void RightClick(int x, int y)
    {
        SetCursorPos(x, y);
        Thread.Sleep(30);
        mouse_event(MouseEventRightDown, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(20);
        mouse_event(MouseEventRightUp, 0, 0, 0, UIntPtr.Zero);
    }

    public void Scroll(int delta) => mouse_event(MouseEventWheel, 0, 0, (uint)delta, UIntPtr.Zero);

    public void TypeText(string text)
    {
        foreach (var c in text)
        {
            keybd_event(0, c, KeyboardUnicode, UIntPtr.Zero);
            keybd_event(0, c, KeyboardUnicode | KeyboardKeyup, UIntPtr.Zero);
        }
    }

    public void PressKey(string key)
    {
        var vk = MapKey(key);
        if (vk == 0) return;
        keybd_event(vk, 0, 0, UIntPtr.Zero);
        keybd_event(vk, 0, KeyboardKeyup, UIntPtr.Zero);
    }

    public (int Width, int Height) GetScreenSize()
        => (GetSystemMetrics(0), GetSystemMetrics(1));

    private static byte MapKey(string key) => key.Trim().ToLowerInvariant() switch
    {
        "enter" or "return" => 0x0D,
        "tab" => 0x09,
        "escape" or "esc" => 0x1B,
        "space" => 0x20,
        "backspace" => 0x08,
        "delete" or "del" => 0x2E,
        "home" => 0x24,
        "end" => 0x23,
        "pageup" or "pgup" => 0x21,
        "pagedown" or "pgdn" => 0x22,
        "up" => 0x26,
        "down" => 0x28,
        "left" => 0x25,
        "right" => 0x27,
        "ctrl+c" => 0x03,
        "ctrl+v" => 0x16,
        "ctrl+x" => 0x18,
        "ctrl+z" => 0x1A,
        "ctrl+a" => 0x01,
        "ctrl+s" => 0x13,
        "alt+tab" => 0x09, // simplified; full Alt+Tab needs both keys
        "f1" => 0x70,
        "f2" => 0x71,
        "f3" => 0x72,
        "f4" => 0x73,
        "f5" => 0x74,
        _ => 0
    };

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, ushort bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
