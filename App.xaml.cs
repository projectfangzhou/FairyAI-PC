using System.Globalization;
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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var sc = new ServiceCollection();
        sc.AddSingleton<IContinuousSpeechService, ContinuousSpeechService>();
        sc.AddSingleton<ILlmService, LlmService>();
        sc.AddSingleton<IChatHistoryService, ChatHistoryService>();
        sc.AddSingleton<IOpenClawManager, OpenClawManager>();
        sc.AddSingleton<AmbientViewModel>();
        _services = sc.BuildServiceProvider();

        _vm = _services.GetRequiredService<AmbientViewModel>();
        await _vm.InitializeAsync();

        _vm.SpeechEnded += OnUserExit;

        _overlay = new AmbientOverlayWindow { DataContext = _vm };
        _overlay.Show();

        StartWakeWordDetection();

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
    }

    private void StartWakeWordDetection()
    {
        try
        {
            var culture = new CultureInfo("en-US");
            _wakeEngine = new SpeechRecognitionEngine(culture);
            var grammar = new Grammar(new GrammarBuilder(new Choices("Fairy")));
            _wakeEngine.LoadGrammar(grammar);
            _wakeEngine.SetInputToDefaultAudioDevice();
            _wakeEngine.SpeechRecognized += OnWakeWord;
            _wakeEngine.RecognizeAsync(RecognizeMode.Multiple);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Wake word init failed: {ex.Message}", "Fairy AI",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnWakeWord(object? sender, SpeechRecognizedEventArgs e)
    {
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
        _inactivityTimer?.Stop();
        _wakeEngine?.RecognizeAsyncStop();
        _wakeEngine?.Dispose();
        _vm?.Dispose();
        base.OnExit(e);
    }
}
