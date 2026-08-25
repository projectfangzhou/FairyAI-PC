using System.Diagnostics;
using System.IO;
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
    private FloatingOrbWindow? _orb;
    private DynamicIslandWindow? _island;
    private FairyViewModel? _vm;
    private DispatcherTimer? _inactivityTimer;
    private const int InactivityTimeoutMs = 30_000;
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Log("=== Fairy AI Starting (Dynamic Island Mode) ===");

        try
        {
            // DI
            var sc = new ServiceCollection();
            sc.AddSingleton<IMiMoAsrService, MiMoAsrService>();
            sc.AddSingleton<IMiMoTtsService, MiMoTtsService>();
            sc.AddSingleton<IAudioPlayerService, AudioPlayerService>();
            sc.AddSingleton<IContinuousSpeechService, HybridSpeechService>();
            sc.AddSingleton<ILlmService, LlmService>();
            sc.AddSingleton<IChatHistoryService, ChatHistoryService>();
            sc.AddSingleton<IOpenClawManager, OpenClawManager>();
            sc.AddSingleton<ITavilySearchService, TavilySearchService>();
            sc.AddSingleton<IOpenClawAgentService, OpenClawAgentService>();
            sc.AddSingleton<IAppMappingService, AppMappingService>();
            sc.AddSingleton<IEverythingService, EverythingService>();
            sc.AddSingleton<IIntentAnalyzer, IntentAnalyzer>();
            sc.AddSingleton<FairyViewModel>();
            _services = sc.BuildServiceProvider();
            Log("DI container built");

            // ViewModel
            _vm = _services.GetRequiredService<FairyViewModel>();
            await _vm.InitializeAsync();
            Log("ViewModel initialized");

            // Windows
            _orb = new FloatingOrbWindow();
            _island = new DynamicIslandWindow();

            _vm.SetWindows(_orb, _island);

            // Orb click → toggle island
            _orb.OrbClicked += () => Dispatcher.Invoke(() => _vm.OnOrbClicked());

            // Show orb (floating at top center)
            _orb.Show();
            Log("Orb window shown");

            // Inactivity timer
            _inactivityTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(InactivityTimeoutMs)
            };
            _inactivityTimer.Tick += (_, _) =>
            {
                _inactivityTimer.Stop();
                Dispatcher.Invoke(() => _vm?.Deactivate());
            };

            Log("Fairy AI ready — click the orb to start");
        }
        catch (Exception ex)
        {
            Log($"FATAL: {ex}");
            MessageBox.Show($"启动失败:\n{ex.Message}", "Fairy AI", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log("=== Fairy AI Shutting Down ===");
        _inactivityTimer?.Stop();
        _vm?.Dispose();
        base.OnExit(e);
    }
}
