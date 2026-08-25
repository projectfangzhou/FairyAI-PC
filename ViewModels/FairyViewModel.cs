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
        IAudioPlayerService audioPlayer)
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
        AudioLevel = level;
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

    private async Task HandleChat(string text)
    {
        await HandleChatInternal(text);
    }

    private async Task HandleChatInternal(string text)
    {
        await UpdateStatus("思考中...");

        var fullResponse = new StringBuilder();
        try
        {
            await foreach (var chunk in _llm.StreamChatAsync(
                GetHistoryMessages(), text,
                CurrentProvider.BaseUrl, CurrentProvider.Model, CurrentProvider.ApiKey))
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

            // TTS
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
                var audio = await _tts.SynthesizeAsync(cleanText);
                if (audio.Length > 0)
                    await _audioPlayer.PlayAsync(audio);
            }
        }
        catch (Exception ex) { Log($"TTS error: {ex.Message}"); }
    }

    private List<ChatMessage> GetHistoryMessages()
    {
        return Messages.TakeLast(20).ToList();
    }

    [ObservableProperty]
    private ObservableCollection<ChatMessage> _messages = new();

    // Provider config
    public LLMProviderConfig CurrentProvider => LLMProviders.Presets[0]; // Default to Kimi

    public void Dispose()
    {
        _speech.SpeechRecognized -= OnSpeechRecognized;
        _speech.SpeechEnded -= OnSpeechEnded;
        _speech.AudioLevel -= OnAudioLevel;
        _speech.Dispose();
        _audioPlayer.Stop();
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }
}
