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

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public AmbientViewModel(
        IContinuousSpeechService speech,
        ILlmService llm,
        IChatHistoryService history,
        IOpenClawManager claw,
        ITavilySearchService search,
        IOpenClawAgentService agent)
    {
        _speech = speech;
        _llm = llm;
        _history = history;
        _claw = claw;
        _search = search;
        _agent = agent;

        _speech.SpeechRecognized += OnSpeechRecognized;
        _speech.SpeechEnded += OnSpeechEnded;
        _speech.AudioLevel += OnAudioLevel;
    }

    public async Task InitializeAsync()
    {
        await _history.InitializeAsync();
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

            // Check if web search is needed
            string? searchContext = null;
            if (NeedsWebSearch(text))
            {
                StatusText = "Searching web...";
                searchContext = await _search.SearchAsync(text);
                Log($"Search context length: {searchContext.Length}");
            }

            // Check if this is an agent request (open app, file operations, system commands)
            if (NeedsAgent(text))
            {
                StatusText = "Executing...";
                var agentResult = await _agent.ExecuteAsync(text);
                var agentMsg = new ChatMessage { Role = "assistant", Content = agentResult };
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => Messages.Add(agentMsg));
                await _history.SaveAsync(_sessionId, agentMsg);
                IsProcessing = false;
                IsListening = true;
                StatusText = "Listening...";
                Log("Agent request completed");
                return;
            }

            IsListening = false;
            IsProcessing = true;
            StatusText = "Processing...";

            // Add placeholder AI message
            var aiMsg = new ChatMessage { Role = "assistant", Content = "..." };
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => Messages.Add(aiMsg));

            // Build effective text with search context
            var effectiveText = searchContext != null
                ? $"[联网搜索结果]\n{searchContext}\n\n[用户问题] {text}"
                : text;

            // Accumulate response locally, update UI periodically
            var fullResponse = new System.Text.StringBuilder();
            int updateCounter = 0;

            await foreach (var chunk in _llm.StreamChatAsync(Messages, effectiveText, Endpoint, Model, ApiKey))
            {
                fullResponse.Append(chunk);
                updateCounter++;

                // Update UI every 5 chunks by replacing message object
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

            // Final update
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
            Log($"LLM final response length: {finalText.Length}");

            // Save to history
            var savedMsg = new ChatMessage { Id = aiMsg.Id, Role = "assistant", Content = aiMsg.Content, Timestamp = aiMsg.Timestamp };
            await _history.SaveAsync(_sessionId, savedMsg);
            Log("LLM response complete");
        }
        catch (Exception ex)
        {
            Log($"LLM ERROR: {ex}");
        }

        IsProcessing = false;
        IsListening = true;
        StatusText = "Listening...";
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
