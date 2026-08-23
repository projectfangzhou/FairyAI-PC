using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyAiAssistant.Models;
using MyAiAssistant.Services;

namespace MyAiAssistant.ViewModels;

public partial class AmbientViewModel : ObservableObject, IDisposable
{
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
    }
    private readonly IContinuousSpeechService _speech;
    private readonly ILlmService _llm;
    private readonly IChatHistoryService _history;
    private readonly IOpenClawManager _claw;
    private readonly ITavilySearchService _search;
    private readonly IOpenClawAgentService _agent;
    private readonly IAppMappingService _mapping;
    private readonly IIntentAnalyzer _intent;
    private readonly string _sessionId = Guid.NewGuid().ToString("N");

    [ObservableProperty] private string _endpoint = "https://api.moonshot.cn/v1/chat/completions";
    [ObservableProperty] private string _model = "kimi-k2.6";
    [ObservableProperty] private string? _apiKey = "sk-AJjPEqZImXxDtZwGg4PwmSy2WBuaTbbs4o8mTHlx4VoqBcIv";
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isListening;
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private float _audioLevel;
    [ObservableProperty] private string _statusText = "Say \"Fairy\" to activate";
    [ObservableProperty] private string _inputText = string.Empty;

    // Pending confirmation state
    private List<string>? _pendingCandidates;
    private string? _pendingKeyword;

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public AmbientViewModel(
        IContinuousSpeechService speech,
        ILlmService llm,
        IChatHistoryService history,
        IOpenClawManager claw,
        ITavilySearchService search,
        IOpenClawAgentService agent,
        IAppMappingService mapping,
        IIntentAnalyzer intent)
    {
        _speech = speech;
        _llm = llm;
        _history = history;
        _claw = claw;
        _search = search;
        _agent = agent;
        _mapping = mapping;
        _intent = intent;

        _speech.SpeechRecognized += OnSpeechRecognized;
        _speech.SpeechEnded += OnSpeechEnded;
        _speech.AudioLevel += OnAudioLevel;
    }

    public async Task InitializeAsync()
    {
        await _history.InitializeAsync();
        await _mapping.InitializeAsync();
        try { _claw.Start(); } catch { /* OpenClaw is optional */ }
    }

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        IsListening = true;
        StatusText = "Listening...";
        _speech.StartListening();
    }

    public void Deactivate()
    {
        IsActive = false;
        IsListening = false;
        IsProcessing = false;
        StatusText = "Say \"Fairy\" to activate";
        _speech.StopListening();
    }

    private async void OnSpeechRecognized(string text)
    {
        Log($"OnSpeechRecognized: '{text}', IsActive={IsActive}");
        if (!IsActive) return;
        await ProcessUserInput(text);
    }

    private void OnSpeechEnded()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Deactivate();
            SpeechEnded?.Invoke();
        });
    }

    private void OnAudioLevel(float level)
    {
        AudioLevel = level;
    }

    public event Action? SpeechEnded;

    [RelayCommand]
    private async Task SendTextAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var msg = text.Trim();
        InputText = string.Empty;

        // Reuse the same LLM call logic as voice
        await ProcessUserInput(msg);
    }

    private async Task ProcessUserInput(string text)
    {
        Log($"ProcessUserInput: '{text}'");
        if (!IsActive)
        {
            IsActive = true;
            StatusText = "Processing...";
        }

        try
        {
            var userMsg = new ChatMessage { Role = "user", Content = text };
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => Messages.Add(userMsg));
            await _history.SaveAsync(_sessionId, userMsg);

            // Step 1: Check if user is confirming a candidate selection (special case)
            if (_pendingCandidates != null)
            {
                if (int.TryParse(text.Trim(), out var choice) && choice >= 1 && choice <= _pendingCandidates.Count)
                {
                    var selectedPath = _pendingCandidates[choice - 1];
                    await _agent.OpenAppAsync(selectedPath);
                    if (_pendingKeyword != null)
                    {
                        await _mapping.SaveMappingAsync(_pendingKeyword, selectedPath);
                        Log($"Saved mapping: {_pendingKeyword} -> {selectedPath}");
                    }
                    var confirmMsg = new ChatMessage { Role = "assistant", Content = $"已打开并记住：{_pendingKeyword} → {Path.GetFileName(selectedPath)}" };
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => Messages.Add(confirmMsg));
                    await _history.SaveAsync(_sessionId, confirmMsg);
                }
                else if (text.Contains("取消") || text.Contains("cancel"))
                {
                    var cancelMsg = new ChatMessage { Role = "assistant", Content = "已取消。" };
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => Messages.Add(cancelMsg));
                }
                else
                {
                    var errMsg = new ChatMessage { Role = "assistant", Content = $"请输入有效编号（1-{_pendingCandidates.Count}）或\"取消\"。" };
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => Messages.Add(errMsg));
                }
                _pendingCandidates = null;
                _pendingKeyword = null;
                IsProcessing = false;
                IsListening = true;
                return;
            }

            // Step 2: AI analyzes intent
            StatusText = "Understanding...";
            var historyText = string.Join("\n", Messages.TakeLast(10).Select(m => $"{m.Role}: {m.Content}"));
            var intent = await _intent.AnalyzeAsync(text, historyText);
            Log($"Intent: {intent.Intent}, query: '{intent.Query}'");

            // Step 3: Route based on AI intent
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

                case "chat":
                default:
                    await HandleChat(text, intent.Reply);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log($"ProcessUserInput ERROR: {ex}");
            var errMsg = new ChatMessage { Role = "assistant", Content = $"出错了: {ex.Message}" };
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => Messages.Add(errMsg));
        }

        IsProcessing = false;
        IsListening = true;
        StatusText = "Listening...";
    }

    private async Task HandleOpenApp(string query)
    {
        StatusText = "Executing...";
        var agentResult = await _agent.ExecuteAsync($"打开{query}");
        var agentMsg = new ChatMessage { Role = "assistant", Content = agentResult.Message };
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => Messages.Add(agentMsg));
        await _history.SaveAsync(_sessionId, agentMsg);

        if (agentResult.NeedsConfirmation)
        {
            _pendingCandidates = agentResult.Candidates;
            _pendingKeyword = agentResult.PendingKeyword;
            StatusText = "请输入编号确认打开";
        }
    }

    private async Task HandleSearchFile(string query)
    {
        StatusText = "Searching files...";
        var agentResult = await _agent.ExecuteAsync($"搜索{query}");
        var agentMsg = new ChatMessage { Role = "assistant", Content = agentResult.Message };
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => Messages.Add(agentMsg));
        await _history.SaveAsync(_sessionId, agentMsg);

        if (agentResult.NeedsConfirmation)
        {
            _pendingCandidates = agentResult.Candidates;
            _pendingKeyword = agentResult.PendingKeyword;
            StatusText = "请输入编号确认打开";
        }
    }

    private async Task HandleSearchWeb(string query)
    {
        StatusText = "Searching web...";
        var searchContext = await _search.SearchAsync(query);

        // Send to LLM with search context
        var aiMsg = new ChatMessage { Role = "assistant", Content = "..." };
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => Messages.Add(aiMsg));

        var effectiveText = $"[联网搜索结果]\n{searchContext}\n\n[用户问题] {query}";
        var fullResponse = new System.Text.StringBuilder();
        int updateCounter = 0;

        await foreach (var chunk in _llm.StreamChatAsync(Messages, effectiveText, Endpoint, Model, ApiKey))
        {
            fullResponse.Append(chunk);
            updateCounter++;
            if (updateCounter % 5 == 0)
            {
                var snapshot = fullResponse.ToString();
                var msgId = aiMsg.Id;
                var msgTs = aiMsg.Timestamp;
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    for (int i = Messages.Count - 1; i >= 0; i--)
                    {
                        if (Messages[i].Id == msgId)
                        {
                            Messages[i] = new ChatMessage { Id = msgId, Role = "assistant", Content = snapshot, Timestamp = msgTs };
                            break;
                        }
                    }
                });
            }
        }

        var finalText = fullResponse.ToString();
        var finalId = aiMsg.Id;
        var finalTs = aiMsg.Timestamp;
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            for (int i = Messages.Count - 1; i >= 0; i--)
            {
                if (Messages[i].Id == finalId)
                {
                    Messages[i] = new ChatMessage { Id = finalId, Role = "assistant", Content = finalText, Timestamp = finalTs };
                    break;
                }
            }
        });
        await _history.SaveAsync(_sessionId, new ChatMessage { Id = aiMsg.Id, Role = "assistant", Content = finalText, Timestamp = finalTs });
    }

    private async Task HandleChat(string text, string? aiReply)
    {
        IsListening = false;
        IsProcessing = true;
        StatusText = "Processing...";

        var aiMsg = new ChatMessage { Role = "assistant", Content = "..." };
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => Messages.Add(aiMsg));

        var fullResponse = new System.Text.StringBuilder();
        int updateCounter = 0;

        await foreach (var chunk in _llm.StreamChatAsync(Messages, text, Endpoint, Model, ApiKey))
        {
            fullResponse.Append(chunk);
            updateCounter++;
            if (updateCounter % 5 == 0)
            {
                var snapshot = fullResponse.ToString();
                var msgId = aiMsg.Id;
                var msgTs = aiMsg.Timestamp;
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    for (int i = Messages.Count - 1; i >= 0; i--)
                    {
                        if (Messages[i].Id == msgId)
                        {
                            Messages[i] = new ChatMessage { Id = msgId, Role = "assistant", Content = snapshot, Timestamp = msgTs };
                            break;
                        }
                    }
                });
            }
        }

        var finalText = fullResponse.ToString();
        var finalId = aiMsg.Id;
        var finalTs = aiMsg.Timestamp;
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            for (int i = Messages.Count - 1; i >= 0; i--)
            {
                if (Messages[i].Id == finalId)
                {
                    Messages[i] = new ChatMessage { Id = finalId, Role = "assistant", Content = finalText, Timestamp = finalTs };
                    break;
                }
            }
        });
        await _history.SaveAsync(_sessionId, new ChatMessage { Id = aiMsg.Id, Role = "assistant", Content = finalText, Timestamp = finalTs });
    }

    private static bool NeedsWebSearch(string text)
    {
        string[] keywords = ["今天", "现在", "最新", "新闻", "天气", "价格", "股票", "最近", "当下", "目前",
            "today", "now", "latest", "news", "weather", "price", "stock", "current"];
        var lower = text.ToLowerInvariant();
        return keywords.Any(k => lower.Contains(k));
    }

    private static bool NeedsAgent(string text)
    {
        string[] keywords = ["打开", "运行", "启动", "关闭", "创建", "删除", "移动", "复制",
            "open", "run", "launch", "close", "create", "delete", "move", "copy",
            "文件夹", "文件", "folder", "file", "程序", "应用", "app", "software",
            "安装", "卸载", "install", "uninstall", "执行", "execute", "命令", "command"];
        var lower = text.ToLowerInvariant();
        return keywords.Any(k => lower.Contains(k));
    }

    public void Dispose()
    {
        _speech.SpeechRecognized -= OnSpeechRecognized;
        _speech.SpeechEnded -= OnSpeechEnded;
        _speech.AudioLevel -= OnAudioLevel;
        _speech.Dispose();
        _claw.Dispose();
    }
}
