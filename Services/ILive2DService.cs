using System.IO;

namespace MyAiAssistant.Services;

public interface ILive2DService
{
    bool IsEnabled { get; }
    bool IsWindowVisible { get; }

    void Initialize();
    void ShowWindow();
    void HideWindow();
    void CloseWindow();

    // Lip sync control
    void SetLipSync(float value);
    void StartLipSync();
    void StopLipSync();

    // Expressions & motions
    void SetExpression(string expression);
    void StartMotion(string motionGroup, int index);
    void StartRandomMotion(string motionGroup);

    // Text-to-speech integration
    void Speak(string text);

    // Window properties
    void SetOpacity(double opacity);
    void SetTopmost(bool topmost);
    void SetClickThrough(bool enable);
}
