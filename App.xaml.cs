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
    private GlobalKeyboardHook? _keyboardHook;
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
            sc.AddSingleton<IMiMoAsrService, MiMoAsrService>();
            sc.AddSingleton<IContinuousSpeechService, HybridSpeechService>();
            sc.AddSingleton<ILlmService, LlmService>();
            sc.AddSingleton<IChatHistoryService, ChatHistoryService>();
            sc.AddSingleton<IOpenClawManager, OpenClawManager>();
            sc.AddSingleton<ITavilySearchService, TavilySearchService>();
            sc.AddSingleton<IOpenClawAgentService, OpenClawAgentService>();
            sc.AddSingleton<IAppMappingService, AppMappingService>();
            sc.AddSingleton<IEverythingService, EverythingService>();
            sc.AddSingleton<IIntentAnalyzer, IntentAnalyzer>();
            sc.AddSingleton<IMiMoTtsService, MiMoTtsService>();
            sc.AddSingleton<IAudioPlayerService, AudioPlayerService>();
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

            // Global keyboard hook: Left Alt toggle
            _keyboardHook = new GlobalKeyboardHook();
            _keyboardHook.LeftAltPressed += OnLeftAltPressed;
            _keyboardHook.Start();
            Log("Global keyboard hook started (Left Alt toggle)");

            // Delay speech recognition start to ensure audio subsystem is ready
            var delayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            delayTimer.Tick += (_, _) =>
            {
                delayTimer.Stop();
                StartWakeWordDetection();
            };
            delayTimer.Start();
            Log("Wake word detection scheduled (2s delay)");

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
            // List available audio devices
            var devices = SpeechRecognitionEngine.InstalledRecognizers();
            Log($"Installed recognizers: {devices.Count}");
            foreach (var r in devices)
                Log($"  - {r.Name} ({r.Culture.Name})");

            // Use en-US recognizer for English wake word "Fairy"
            var enUS = devices.FirstOrDefault(r => r.Culture.Name == "en-US");
            if (enUS != null)
            {
                _wakeEngine = new SpeechRecognitionEngine(enUS.Id);
                Log($"Using en-US recognizer: {enUS.Name}");
            }
            else
            {
                _wakeEngine = new SpeechRecognitionEngine();
                Log($"Using default recognizer: {_wakeEngine.RecognizerInfo.Name}");
            }

            // English wake words for en-US recognizer
            var choices = new Choices("Fairy", "fairy");
            var grammarBuilder = new GrammarBuilder(choices)
            {
                Culture = new CultureInfo("en-US")
            };
            var grammar = new Grammar(grammarBuilder);
            _wakeEngine.LoadGrammar(grammar);
            Log("Grammar loaded");

            _wakeEngine.SetInputToDefaultAudioDevice();
            Log("Audio device set");

            _wakeEngine.SpeechRecognized += OnWakeWord;
            _wakeEngine.AudioLevelUpdated += (_, e) =>
            {
                if (e.AudioLevel > 0)
                    Log($"Audio level: {e.AudioLevel}");
            };
            _wakeEngine.SpeechDetected += (_, _) => Log("Speech detected!");
            _wakeEngine.RecognizeCompleted += (_, e) => Log($"Recognize completed, result: {e.Result?.Text ?? "null"}");

            _wakeEngine.RecognizeAsync(RecognizeMode.Multiple);
            Log("RecognizeAsync(Multiple) called - listening...");
        }
        catch (Exception ex)
        {
            Log($"Wake word init FAILED: {ex}");
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

    private bool _isOverlayVisible;

    private void OnLeftAltPressed()
    {
        Log($"Left Alt pressed (toggle: !{_isOverlayVisible})");
        Dispatcher.Invoke(() =>
        {
            if (_isOverlayVisible)
            {
                _vm?.Deactivate();
                _overlay?.DeactivateOverlay();
                _isOverlayVisible = false;
            }
            else
            {
                _vm?.Activate();
                _overlay?.ActivateOverlay();
                _inactivityTimer?.Stop();
                _inactivityTimer?.Start();
                _isOverlayVisible = true;
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log("=== Fairy AI Shutting Down ===");
        _inactivityTimer?.Stop();
        _keyboardHook?.Dispose();
        _wakeEngine?.RecognizeAsyncStop();
        _wakeEngine?.Dispose();
        _vm?.Dispose();
        base.OnExit(e);
    }
}
