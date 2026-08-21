using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Speech.Recognition;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using MyAiAssistant.Services;
using MyAiAssistant.ViewModels;
using MyAiAssistant.Windows;

namespace MyAiAssistant;

public partial class App : Application
{
    private IServiceProvider _services = null!;
    private AmbientOverlayWindow? _overlay;
    private AmbientViewModel? _vm;
    private SpeechRecognitionEngine? _wakeEngine;
    private DispatcherTimer? _inactivityTimer;
    private const int InactivityTimeoutMs = 30_000;
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    private static void Log(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {msg}\n";
        File.AppendAllText(LogPath, line);
        Debug.Write(line);
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Log("=== Fairy AI Starting ===");

        try
        {
            var sc = new ServiceCollection();
            sc.AddSingleton<IContinuousSpeechService, ContinuousSpeechService>();
            sc.AddSingleton<ILlmService, LlmService>();
            sc.AddSingleton<IChatHistoryService, ChatHistoryService>();
            sc.AddSingleton<IOpenClawManager, OpenClawManager>();
            sc.AddSingleton<AmbientViewModel>();
            _services = sc.BuildServiceProvider();
            Log("DI container built");

            _vm = _services.GetRequiredService<AmbientViewModel>();
            Log("ViewModel resolved");

            await _vm.InitializeAsync();
            Log("ViewModel initialized (DB + OpenClaw)");

            _vm.SpeechEnded += OnUserExit;

            _overlay = new AmbientOverlayWindow { DataContext = _vm };
            _overlay.Show();
            Log("Overlay window shown");

            StartWakeWordDetection();
            Log("Wake word detection started");

            _inactivityTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(InactivityTimeoutMs)
            };
            _inactivityTimer.Tick += (_, _) =>
            {
                _inactivityTimer.Stop();
                _vm?.Deactivate();
                _overlay?.DeactivateOverlay();
            };
            Log("Inactivity timer configured");

            MessageBox.Show(
                "Fairy AI 已启动！\n\n" +
                "请说 \"Fairy\" 唤醒我。\n" +
                "说 \"Exit\" 关闭对话面板。\n\n" +
                "此窗口关闭后，我将在后台等待唤醒。",
                "Fairy AI",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Log("Startup MessageBox shown");
        }
        catch (Exception ex)
        {
            Log($"FATAL: {ex}");
            MessageBox.Show($"启动失败:\n{ex.Message}\n\n详见 fairy.log", "Fairy AI - Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StartWakeWordDetection()
    {
        try
        {
            // Use system default culture (Chinese if that's what's installed)
            _wakeEngine = new SpeechRecognitionEngine();

            // Support both English "Fairy" and Chinese "精灵" wake words
            var choices = new Choices("Fairy", "fairy", "精灵", "小精灵");
            var grammar = new Grammar(new GrammarBuilder(choices));
            _wakeEngine.LoadGrammar(grammar);
            _wakeEngine.SetInputToDefaultAudioDevice();
            _wakeEngine.SpeechRecognized += OnWakeWord;
            _wakeEngine.RecognizeAsync(RecognizeMode.Multiple);
            Log($"Speech engine started, culture: {_wakeEngine.RecognizerInfo.Culture.Name}");
        }
        catch (Exception ex)
        {
            Log($"Wake word init FAILED: {ex.Message}");
            MessageBox.Show($"语音识别初始化失败: {ex.Message}\n\n请检查麦克风权限。",
                "Fairy AI", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnWakeWord(object? sender, SpeechRecognizedEventArgs e)
    {
        Log($"Wake word detected: '{e.Result.Text}' confidence={e.Result.Confidence:F2}");
        if (e.Result.Confidence < 0.6f) return;

        Dispatcher.Invoke(() =>
        {
            _vm?.Activate();
            _overlay?.ActivateOverlay();
            _inactivityTimer?.Stop();
            _inactivityTimer?.Start();
        });
    }

    private void OnUserExit()
    {
        Dispatcher.Invoke(() =>
        {
            _vm?.Deactivate();
            _overlay?.DeactivateOverlay();
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log("=== Fairy AI Shutting Down ===");
        _inactivityTimer?.Stop();
        _wakeEngine?.RecognizeAsyncStop();
        _wakeEngine?.Dispose();
        _vm?.Dispose();
        base.OnExit(e);
    }
}
