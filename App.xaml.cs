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
    private TrayIconService? _tray;
    private ClipboardHistoryService? _clipboardHistory;
    private ClipboardHistoryWindow? _clipboardWindow;
    private OllamaSetupWindow? _ollamaWindow;
    private GlobalKeyboardHook? _keyboardHook;
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
            // Check for setup-complete flag (from installer) — show welcome but DON'T skip wizard
            if (e.Args.Contains("--setup-complete"))
            {
                Log("Installer finished — will show setup wizard if needed");
            }

            // Always show setup wizard on first run (or when API key is missing)
            if (ConfigManager.NeedsSetup())
            {
                Log("First run detected — showing setup wizard");
                var wizard = new SetupWizardWindow();
                var result = wizard.ShowDialog();
                if (result != true)
                {
                    Log("Setup cancelled by user");
                    Shutdown();
                    return;
                }
                Log("Setup completed");
            }

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
            sc.AddSingleton<IVisionService, VisionService>();
            sc.AddSingleton<ILive2DService, Live2DService>();
            sc.AddSingleton<IScreenControlService, ScreenControlService>();
            sc.AddSingleton<IAudioConverter, FfmpegAudioConverter>();
            sc.AddSingleton<VectorDatabase>();
            sc.AddSingleton<RAGService>();
            sc.AddSingleton<AgentService>();
            sc.AddSingleton<MCPClient>();
            sc.AddSingleton<LocalModelService>();
            sc.AddSingleton<SecurityAuditService>();
            sc.AddSingleton<WebDashboardService>();
            sc.AddSingleton<ConnectivityManager>();
            sc.AddSingleton<FairyViewModel>();

            // Function Calling tools & services
            var toolRegistry = new ToolRegistry();
            toolRegistry.Register(new GetCurrentTimeTool());
            toolRegistry.Register(new CalculatorTool());
            toolRegistry.Register(new SystemInfoTool());
            toolRegistry.Register(new ClipboardCopyTool());
            var noteService = new NoteService();
            var reminderService = new ReminderService();
            var shortcutService = new ShortcutService();
            var tokenUsageService = new TokenUsageService();
            var skillService = new SkillService();
            var translateService = new TranslateService();
            toolRegistry.Register(new CreateNoteTool(noteService));
            toolRegistry.Register(new SearchNotesTool(noteService));
            toolRegistry.Register(new ListNotesTool(noteService));
            toolRegistry.Register(new DeleteNoteTool(noteService));
            toolRegistry.Register(new SetReminderTool(reminderService));
            toolRegistry.Register(new ListRemindersTool(reminderService));
            toolRegistry.Register(new CancelReminderTool(reminderService));
            toolRegistry.Register(new CreateShortcutTool(shortcutService));
            toolRegistry.Register(new ListShortcutsTool(shortcutService));
            toolRegistry.Register(new TranslateTool(translateService));
            toolRegistry.Register(new GetTokenUsageTool(tokenUsageService));
            toolRegistry.Register(new SetTokenLimitTool(tokenUsageService));
            toolRegistry.Register(new ListSkillsTool(skillService));
            toolRegistry.Register(new RunSkillTool(skillService));
            var screenControl = new ScreenControlService();
            var visionService = new VisionService();
            var blenderService = new BlenderService();
            var officeService = new OfficeIntegrationService();
            var kbService = new KnowledgeBaseService();
            toolRegistry.Register(new TakeScreenshotTool(visionService));
            toolRegistry.Register(new MouseClickTool(screenControl));
            toolRegistry.Register(new MouseDoubleClickTool(screenControl));
            toolRegistry.Register(new MouseRightClickTool(screenControl));
            toolRegistry.Register(new TypeTextTool(screenControl));
            toolRegistry.Register(new PressKeyTool(screenControl));
            toolRegistry.Register(new ScrollTool(screenControl));
            toolRegistry.Register(new ConvertImageTo3DTool(blenderService));
            toolRegistry.Register(new OpenOfficeDocTool(officeService, kbService));
            toolRegistry.Register(new GetOfficeInfoTool(officeService));
            sc.AddSingleton(toolRegistry);
            sc.AddSingleton(sp => new FunctionCallingService(
                sp.GetRequiredService<ILlmService>(), toolRegistry));
            sc.AddSingleton(noteService);
            sc.AddSingleton(reminderService);
            sc.AddSingleton(shortcutService);
            sc.AddSingleton(tokenUsageService);
            sc.AddSingleton(skillService);
            sc.AddSingleton(translateService);
            _services = sc.BuildServiceProvider();
            Log("DI container built");

            // Start Web Dashboard
            var dashboard = _services.GetRequiredService<WebDashboardService>();
            _ = dashboard.StartAsync();
            Log("Web dashboard starting on port 8080");

            // Log security event
            var auditService = _services.GetRequiredService<SecurityAuditService>();
            auditService.LogEvent("APP_START", "FairyAI v2.0.0 started");

            // Auto-deploy Blender addon
            BlenderService.DeployAddon();

            // ViewModel
            _vm = _services.GetRequiredService<FairyViewModel>();
            await _vm.InitializeAsync();
            Log("ViewModel initialized");

            // Start connectivity services
            var syncManager = _services.GetRequiredService<ConnectivityManager>();
            await syncManager.StartAllAsync();
            Log("Connectivity services started");

            // Windows
            _orb = new FloatingOrbWindow();
            _island = new DynamicIslandWindow();

            _vm.SetWindows(_orb, _island);

            // Orb click → toggle island
            _orb.OrbClicked += () => Dispatcher.Invoke(() => _vm.OnOrbClicked());

            // Show orb (floating at top center)
            _orb.Show();
            Log("Orb window shown");

            // Intercept window close → minimize to tray instead of exiting
            _orb.Closing += (_, _) => MinimizeToTray();
            _island.Closing += (_, _) => MinimizeToTray();

            // Clipboard history
            _clipboardHistory = new ClipboardHistoryService();
            _clipboardHistory.Start();
            Log("Clipboard history started");

            // System tray
            _tray = new TrayIconService(
                _orb, _island,
                enable => AutoStartService.SetEnabled(enable),
                ShowClipboardHistory,
                ShowOllamaSetup);
            _tray.ExitRequested += () => Dispatcher.Invoke(Shutdown);
            _tray.Show();
            Log("Tray icon shown");

            // Start reminder service
            reminderService.ReminderFired += OnReminderFired;
            reminderService.Start();
            Log("Reminder service started");

            // Token usage limit alerts
            tokenUsageService.LimitAlert += (title, msg) =>
            {
                _tray?.Notify(title, msg, System.Windows.Forms.ToolTipIcon.Warning);
                Dispatcher.Invoke(() => MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Warning));
            };

            // Global hotkey: Alt+Space to summon/hide
            _keyboardHook = new GlobalKeyboardHook();
            _keyboardHook.AltSpacePressed += () => Dispatcher.Invoke(ToggleMainVisibility);
            _keyboardHook.Start();
            Log("Global hotkey Alt+Space registered");

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

    private void MinimizeToTray()
    {
        // Cancel close, hide to tray instead
        Dispatcher.Invoke(() =>
        {
            _orb?.Hide();
            _island?.Hide();
            _vm?.Deactivate();
            _tray?.Notify("Fairy AI 仍在运行", "已最小化到系统托盘，双击图标恢复", System.Windows.Forms.ToolTipIcon.Info);
            Log("Minimized to tray");
        });
    }

    /// <summary>Toggle main windows visibility (used by Alt+Space hotkey).</summary>
    private void ToggleMainVisibility()
    {
        if (_orb == null) return;

        if (_orb.IsVisible || _island?.IsVisible == true)
        {
            _orb.Hide();
            _island?.Hide();
            _vm?.Deactivate();
            Log("Hidden via Alt+Space");
        }
        else
        {
            _orb.Show();
            Log("Shown via Alt+Space");
        }
    }

    private void OnReminderFired(ReminderItem reminder)
    {
        Dispatcher.Invoke(() =>
        {
            _tray?.Notify($"提醒: {reminder.Title}", reminder.Message, System.Windows.Forms.ToolTipIcon.Info);
            _island?.ShowResponse($"⏰ 提醒: {reminder.Title}\n{reminder.Message}");
        });
    }

    private void ShowClipboardHistory()
    {
        Dispatcher.Invoke(() =>
        {
            if (_clipboardWindow == null)
            {
                _clipboardWindow = new ClipboardHistoryWindow(_clipboardHistory!);
                _clipboardWindow.Closed += (_, _) => _clipboardWindow = null;
            }
            _clipboardWindow.Show();
            _clipboardWindow.Activate();
        });
    }

    private void ShowOllamaSetup()
    {
        Dispatcher.Invoke(() =>
        {
            if (_ollamaWindow == null)
            {
                _ollamaWindow = new OllamaSetupWindow();
                _ollamaWindow.Closed += (_, _) => _ollamaWindow = null;
            }
            _ollamaWindow.Show();
            _ollamaWindow.Activate();
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log("=== Fairy AI Shutting Down ===");
        _inactivityTimer?.Stop();
        _keyboardHook?.Dispose();
        _tray?.Dispose();
        _clipboardHistory?.Dispose();
        var syncManager = _services?.GetService<ConnectivityManager>();
        syncManager?.Dispose();
        _vm?.Dispose();
        base.OnExit(e);
    }
}
