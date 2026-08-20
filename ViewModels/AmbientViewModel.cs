using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyAiAssistant.Models;
using MyAiAssistant.Services;

namespace MyAiAssistant.ViewModels;

public partial class AmbientViewModel : ObservableObject, IDisposable
{
    private readonly IContinuousSpeechService _speech;
    private readonly ILlmService _llm;
    private readonly IChatHistoryService _history;
    private readonly IOpenClawManager _claw;
    private readonly string _sessionId = Guid.NewGuid().ToString("N");

    [ObservableProperty] private string _endpoint = "http://127.0.0.1:8787/v1/chat/completions";
    [ObservableProperty] private string _model = "llama3";
    [ObservableProperty] private string? _apiKey;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isListening;
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private float _audioLevel;
    [ObservableProperty] private string _statusText = "Say \"Fairy\" to activate";

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public AmbientViewModel(
        IContinuousSpeechService speech,
        ILlmService llm,
        IChatHistoryService history,
        IOpenClawManager claw)
    {
        _speech = speech;
        _llm = llm;
        _history = history;
        _claw = claw;

        _speech.SpeechRecognized += OnSpeechRecognized;
        _speech.SpeechEnded += OnSpeechEnded;
        _speech.AudioLevel += OnAudioLevel;
    }

    public async Task InitializeAsync()
    {
        await _history.InitializeAsync();
        _claw.Start();
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
        if (!IsActive) return;

        var userMsg = new ChatMessage { Role = "user", Content = text };
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => Messages.Add(userMsg));
        await _history.SaveAsync(_sessionId, userMsg);

        IsListening = false;
        IsProcessing = true;
        StatusText = "Processing...";

        var aiMsg = new ChatMessage { Role = "assistant", Content = "" };
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => Messages.Add(aiMsg));

        try
        {
            await foreach (var chunk in _llm.StreamChatAsync(Messages, text, Endpoint, Model, ApiKey))
            {
                aiMsg.Content += chunk;
                var snapshot = aiMsg.Content;
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var idx = Messages.IndexOf(aiMsg);
                    if (idx >= 0)
                    {
                        Messages.RemoveAt(idx);
                        Messages.Insert(idx, new ChatMessage
                        {
                            Id = aiMsg.Id, Role = "assistant",
                            Content = snapshot, Timestamp = aiMsg.Timestamp
                        });
                    }
                });
            }
            await _history.SaveAsync(_sessionId, aiMsg);
        }
        catch (Exception ex)
        {
            aiMsg.Content = $"Error: {ex.Message}";
        }

        IsProcessing = false;
        IsListening = true;
        StatusText = "Listening...";
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

    public void Dispose()
    {
        _speech.SpeechRecognized -= OnSpeechRecognized;
        _speech.SpeechEnded -= OnSpeechEnded;
        _speech.AudioLevel -= OnAudioLevel;
        _speech.Dispose();
        _claw.Dispose();
    }
}
