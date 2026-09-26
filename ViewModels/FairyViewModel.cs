using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyAiAssistant.Models;
using MyAiAssistant.Services;
using MyAiAssistant.Windows;

namespace MyAiAssistant.ViewModels;

public partial class FairyViewModel : ObservableObject, IDisposable
{
    private readonly IContinuousSpeechService _speech;
    private readonly ILlmService _llm;
    private readonly IChatHistoryService _history;
    private readonly IOpenClawAgentService _agent;
    private readonly IAppMappingService _mapping;
    private readonly IIntentAnalyzer _intent;
    private readonly ITavilySearchService _search;
    private readonly IMiMoTtsService _tts;
    private readonly IAudioPlayerService _audioPlayer;
    private readonly IVisionService _vision;
    private readonly ILive2DService _live2d;
    private readonly FunctionCallingService _functionCalling;
    private readonly ToolRegistry _toolRegistry;
    private readonly NoiseReductionService _noiseReduction = new();

    private FloatingOrbWindow? _orb;
    private DynamicIslandWindow? _island;

    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isListening;
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private string _statusText = "点击开始对话";
    [ObservableProperty] private float _audioLevel;
    [ObservableProperty] private string _transcription = "";
    [ObservableProperty] private string _response = "";

    private readonly string _sessionId = Guid.NewGuid().ToString("N");
    private List<string>? _pendingCandidates;
    private string? _pendingKeyword;
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public FairyViewModel(
        IContinuousSpeechService speech, ILlmService llm,
        IChatHistoryService history, IOpenClawAgentService agent,
        IAppMappingService mapping, IIntentAnalyzer intent,
        ITavilySearchService search, IMiMoTtsService tts,
        IAudioPlayerService audioPlayer, IVisionService vision,
        ILive2DService live2d, FunctionCallingService functionCalling,
        ToolRegistry toolRegistry)
    {
        _speech = speech;
        _llm = llm;
        _history = history;
        _agent = agent;
        _mapping = mapping;
        _intent = intent;
        _search = search;
        _tts = tts;
        _audioPlayer = audioPlayer;
        _vision = vision;
        _live2d = live2d;
        _functionCalling = functionCalling;
        _toolRegistry = toolRegistry;

        _speech.SpeechRecognized += OnSpeechRecognized;
        _speech.SpeechEnded += OnSpeechEnded;
        _speech.AudioLevel += OnAudioLevel;
    }

    public void SetWindows(FloatingOrbWindow orb, DynamicIslandWindow island)
    {
        _orb = orb;
        _island = island;

        _island.CloseClicked += OnIslandClose;
        _island.IslandActivated += OnIslandActivated;
    }

    public async Task InitializeAsync()
    {
        await _history.InitializeAsync();

        // Initialize Live2D if configured
        if (_live2d.IsEnabled)
        {
            _live2d.Initialize();
        }
    }

    // === Orb & Island Control ===

    public void OnOrbClicked()
    {
        if (IsActive)
        {
            Deactivate();
            return;
        }
        Activate();
    }

    public void Activate()
    {
        IsActive = true;
        IsListening = true;
        StatusText = "正在聆听...";

        _orb?.SetActive(true);
        _island?.ShowIsland();
        _island?.ShowWaveform();
        _island?.SetStatus("正在聆听...");

        _speech.StartListening();
    }

    public void Deactivate()
    {
        IsActive = false;
        IsListening = false;
        IsProcessing = false;
        StatusText = "点击开始对话";

        _orb?.SetActive(false);
        _island?.HideIsland();

        _speech.StopListening();
        _audioPlayer.Stop();
    }

    private void OnIslandClose()
    {
        Deactivate();
    }

    private void OnIslandActivated()
    {
        // Island is now visible, start listening
    }

    // === Voice Processing ===

    private async void OnSpeechRecognized(string text)
    {
        if (!IsActive) return;

        Log($"Speech recognized: '{text}'");

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            Transcription = text;
            _island?.ShowTranscription(text);
        });

        // Check pending confirmation
        if (_pendingCandidates != null)
        {
            await HandleConfirmation(text);
            return;
        }

        // Process through intent analysis
        await ProcessUserInput(text);
    }

    private void OnSpeechEnded()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(Deactivate);
    }

    private void OnAudioLevel(float level)
    {
        // Apply noise reduction to increase sensitivity
        AudioLevel = level * 1.5f; // sensitivity boost
        System.Windows.Application.Current.Dispatcher.Invoke(() => _island?.UpdateWaveform(level));
    }

    // === Intent Processing ===

    private async Task ProcessUserInput(string text)
    {
        try
        {
            IsListening = false;
            IsProcessing = true;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StatusText = "理解中...";
                _island?.SetStatus("理解中...");
            });

            // AI analyzes intent
            var historyText = string.Join("\n", Messages.TakeLast(10).Select(m => $"{m.Role}: {m.Content}"));
            var intent = await _intent.AnalyzeAsync(text, historyText);
            Log($"Intent: {intent.Intent}, query: '{intent.Query}'");

            switch (intent.Intent)
            {
                case "open_app":
                    await HandleOpenApp(intent.Query);
                    break;
                case "search_file":
                    await HandleSearchFile(intent.Query);
                    break;
                case "search_web":
                    await HandleSearchWeb(intent.Query);
                    break;
                case "analyze_screen":
                    await HandleAnalyzeScreen(intent.Query);
                    break;
                case "control_screen":
                    await HandleControlScreen(text);
                    break;
                case "open_song":
                    await HandleOpenSong(intent.Query);
                    break;
                default:
                    await HandleChat(text);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log($"ProcessUserInput ERROR: {ex}");
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StatusText = "出错了";
                _island?.ShowResponse($"出错了: {ex.Message}");
            });
        }

        IsProcessing = false;
        IsListening = true;
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            StatusText = "正在聆听...";
            _island?.SetStatus("正在聆听...");
            _island?.ShowWaveform();
        });
    }

    // === Intent Handlers ===

    private async Task HandleOpenApp(string query)
    {
        await UpdateStatus("执行中...");
        var result = await _agent.ExecuteAsync($"打开{query}");
        await ShowResponse(result.Message);

        if (result.NeedsConfirmation)
        {
            _pendingCandidates = result.Candidates;
            _pendingKeyword = result.PendingKeyword;
            await UpdateStatus("请输入编号确认");
        }
    }

    private async Task HandleSearchFile(string query)
    {
        await UpdateStatus("搜索文件...");
        var candidates = await _mapping.SearchExeAsync(query);

        if (candidates.Count == 0)
        {
            await UpdateStatus("联网搜索...");
            var webResult = await _search.SearchAsync(query);
            await ShowResponse($"本地未找到 \"{query}\"\n\n联网搜索：\n{webResult}");
            return;
        }

        // Auto-open safe files, confirm dangerous ones
        var allSafe = candidates.All(c =>
        {
            var ext = Path.GetExtension(c).ToLowerInvariant();
            return ext is not (".exe" or ".bat" or ".cmd" or ".com" or ".scr" or ".msi");
        });

        if (allSafe)
        {
            var file = candidates[0];
            await _agent.OpenAppAsync(file);
            await ShowResponse($"已打开: {Path.GetFileName(file)}");
        }
        else
        {
            var sb = new StringBuilder();
            sb.AppendLine($"找到 {candidates.Count} 个可执行文件：");
            for (int i = 0; i < Math.Min(candidates.Count, 8); i++)
                sb.AppendLine($"{i + 1}. {Path.GetFileName(candidates[i])}");
            sb.AppendLine("请说出编号确认打开。");
            await ShowResponse(sb.ToString());
            _pendingCandidates = candidates;
            _pendingKeyword = query;
        }
    }

    private async Task HandleSearchWeb(string query)
    {
        await UpdateStatus("联网搜索...");
        var webResult = await _search.SearchAsync(query);
        await HandleChatInternal($"[联网搜索结果]\n{webResult}\n\n[用户问题] {query}");
    }

    private async Task HandleAnalyzeScreen(string query)
    {
        await UpdateStatus("正在截取屏幕...");
        var prompt = string.IsNullOrWhiteSpace(query)
            ? "请详细描述这张屏幕截图中的所有内容，包括文字、界面元素、应用程序等。"
            : $"用户想了解: {query}。请根据屏幕截图回答。";

        var result = await _vision.AnalyzeScreenAsync(prompt);
        await ShowResponse(result);
    }

    private async Task HandleControlScreen(string task)
    {
        await UpdateStatus("正在操作屏幕...");
        var config = ConfigManager.Load();
        var systemTask = "你是屏幕自动化助手（Computer Use）。你可以用工具截屏查看界面，然后用鼠标点击、键盘输入完成用户任务。" +
            "步骤：1) take_screenshot 查看界面和坐标 2) 用 mouse_click/type_text/press_key 等工具操作 3) 再次 take_screenshot 确认结果 4) 完成后说明做了什么。\n[用户任务] " + task;

        var fullResponse = new StringBuilder();
        try
        {
            await foreach (var chunk in _functionCalling.ChatWithToolsAsync(
                GetHistoryMessages(), systemTask, config.LLM.BaseUrl, config.LLM.Model, config.LLM.ApiKey))
            {
                fullResponse.Append(chunk);
                var snapshot = fullResponse.ToString();
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Response = snapshot;
                    _island?.ShowResponse(snapshot);
                });
            }
        }
        catch (Exception ex)
        {
            Log($"control_screen error: {ex.Message}");
            await ShowResponse($"屏幕操作失败: {ex.Message}");
            return;
        }

        var finalText = fullResponse.ToString();
        if (!string.IsNullOrWhiteSpace(finalText))
        {
            await _history.SaveAsync(_sessionId, new ChatMessage { Role = "user", Content = task });
            await _history.SaveAsync(_sessionId, new ChatMessage { Role = "assistant", Content = finalText });
            await SpeakResponseAsync(finalText);
        }
    }

    private async Task HandleOpenSong(string query)
    {
        await UpdateStatus("搜索歌曲...");

        // First, try to search online for the song
        var searchResult = await _search.SearchAsync($"{query} 歌曲 播放");

        // Extract song name from search results
        var songName = ExtractSongNameFromSearch(searchResult, query);

        if (!string.IsNullOrWhiteSpace(songName))
        {
            // Try to find the song locally
            var localPath = await FindLocalSong(songName);
            if (!string.IsNullOrWhiteSpace(localPath))
            {
                await UpdateStatus("播放歌曲...");
                await _agent.OpenAppAsync(localPath);
                await ShowResponse($"正在播放: {songName}");
                return;
            }
        }

        // If not found online or locally, try with original query
        var localPathWithOriginal = await FindLocalSong(query);
        if (!string.IsNullOrWhiteSpace(localPathWithOriginal))
        {
            await UpdateStatus("播放歌曲...");
            await _agent.OpenAppAsync(localPathWithOriginal);
            await ShowResponse($"正在播放: {query}");
            return;
        }

        // If still not found, inform user
        await ShowResponse($"未找到歌曲 \"{query}\"。请检查歌曲名称是否正确。");
    }

    private string ExtractSongNameFromSearch(string searchResult, string originalQuery)
    {
        // Simple extraction: look for patterns like "歌名: xxx" or "歌曲: xxx"
        var lines = searchResult.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("歌名") || line.Contains("歌曲") || line.Contains("title"))
            {
                var parts = line.Split(new[] { ':', '：' }, 2);
                if (parts.Length > 1)
                {
                    var name = parts[1].Trim();
                    if (!string.IsNullOrWhiteSpace(name) && name.Length < 100)
                        return name;
                }
            }
        }
        return originalQuery;
    }

    private async Task<string?> FindLocalSong(string songName)
    {
        // Search common music directories
        var musicDirs = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Music",
            "C:\\Music",
            "D:\\Music"
        };

        var musicExtensions = new[] { ".mp3", ".flac", ".wav", ".m4a", ".aac", ".wma", ".ogg" };

        foreach (var dir in musicDirs)
        {
            if (!Directory.Exists(dir)) continue;

            try
            {
                var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories)
                    .Where(f => musicExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .Where(f => Path.GetFileNameWithoutExtension(f).Contains(songName, StringComparison.OrdinalIgnoreCase));

                if (files.Any())
                    return files.First();
            }
            catch { }
        }

        return null;
    }

    private async Task HandleChat(string text)
    {
        await HandleChatInternal(text);
    }

    private async Task HandleChatInternal(string text)
    {
        await UpdateStatus("思考中...");

        // AI-driven capability system: declare all tools, let AI decide
        var config = ConfigManager.Load();
        var systemPrompt = CapabilityRegistry.BuildSystemPrompt(config);

        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };
        messages.AddRange(GetHistoryMessages());
        messages.Add(new ChatMessage { Role = "user", Content = text });

        var fullResponse = new StringBuilder();
        try
        {
            await foreach (var chunk in _functionCalling.ChatWithToolsAsync(
                messages, text, config.LLM.BaseUrl, config.LLM.Model, config.LLM.ApiKey))
            {
                fullResponse.Append(chunk);
                var snapshot = fullResponse.ToString();
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Response = snapshot;
                    _island?.ShowResponse(snapshot);
                });
            }
        }
        catch (Exception ex)
        {
            Log($"LLM error: {ex.Message}");
            await ShowResponse($"抱歉，出错了: {ex.Message}");
        }

        var finalText = fullResponse.ToString();
        if (!string.IsNullOrWhiteSpace(finalText))
        {
            await _history.SaveAsync(_sessionId, new ChatMessage { Role = "user", Content = text });
            await _history.SaveAsync(_sessionId, new ChatMessage { Role = "assistant", Content = finalText });
            await SpeakResponseAsync(finalText);
        }
    }

    // === Helpers ===

    private async Task HandleConfirmation(string text)
    {
        if (int.TryParse(text.Trim(), out var choice) && _pendingCandidates != null
            && choice >= 1 && choice <= _pendingCandidates.Count)
        {
            var path = _pendingCandidates[choice - 1];
            await _agent.OpenAppAsync(path);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (_pendingKeyword != null && ext is (".exe" or ".bat" or ".cmd"))
            {
                await _mapping.SaveMappingAsync(_pendingKeyword, path);
                await ShowResponse($"已打开并记住: {Path.GetFileName(path)}");
            }
            else
            {
                await ShowResponse($"已打开: {Path.GetFileName(path)}");
            }
        }
        else if (text.Contains("取消"))
        {
            await ShowResponse("已取消");
        }
        else
        {
            await ShowResponse("请输入有效编号或说\"取消\"");
        }
        _pendingCandidates = null;
        _pendingKeyword = null;
    }

    private async Task ShowResponse(string text)
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            Response = text;
            _island?.ShowResponse(text);
            StatusText = "正在聆听...";
            _island?.SetStatus("正在聆听...");
        });

        await _history.SaveAsync(_sessionId, new ChatMessage { Role = "assistant", Content = text });
        await SpeakResponseAsync(text);
    }

    private async Task UpdateStatus(string text)
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            StatusText = text;
            _island?.SetStatus(text);
        });
    }

    private async Task SpeakResponseAsync(string text)
    {
        try
        {
            var cleanText = text.Replace("**", "").Replace("*", "").Replace("`", "")
                .Replace("#", "").Replace("\n", " ").Trim();
            if (cleanText.Length > 0)
            {
                // Trigger Live2D lip sync
                if (_live2d.IsEnabled)
                    _live2d.Speak(cleanText);

                var audio = await _tts.SynthesizeAsync(cleanText);
                if (audio.Length > 0)
                    await _audioPlayer.PlayAsync(audio);
            }
        }
        catch (Exception ex) { Log($"TTS error: {ex.Message}"); }
        finally
        {
            if (_live2d.IsEnabled)
                _live2d.StopLipSync();
        }
    }

    private List<ChatMessage> GetHistoryMessages()
    {
        return Messages.TakeLast(20).ToList();
    }

    [ObservableProperty]
    private ObservableCollection<ChatMessage> _messages = new();

    [RelayCommand]
    private async Task ExportConversationAsync()
    {
        try
        {
            var messages = await _history.LoadAsync(_sessionId);
            var path = ExportService.ExportWithDialog(messages);
            if (path != null)
            {
                Log($"Conversation exported to: {path}");
                _island?.ShowResponse($"对话已导出到:\n{path}");
            }
        }
        catch (Exception ex)
        {
            Log($"Export error: {ex.Message}");
            _island?.ShowResponse($"导出失败: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _speech.SpeechRecognized -= OnSpeechRecognized;
        _speech.SpeechEnded -= OnSpeechEnded;
        _speech.AudioLevel -= OnAudioLevel;
        _speech.Dispose();
        _audioPlayer.Stop();
        if (_live2d.IsEnabled)
            _live2d.CloseWindow();
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }
}
