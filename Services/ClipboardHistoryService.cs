using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace MyAiAssistant.Services;

public class ClipboardEntry
{
    public string Text { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

/// <summary>
/// Monitors clipboard text changes and keeps a rolling history (default 50 entries),
/// persisted to JSON in the app directory.
/// </summary>
public class ClipboardHistoryService : IDisposable
{
    private DispatcherTimer? _timer;
    private readonly List<ClipboardEntry> _entries = new();
    private string _lastClipboardText = "";
    private bool _isEnabled;
    private readonly string _historyPath;
    private readonly string _logPath;
    private const int MaxEntries = 50;
    private const int CheckIntervalMs = 800;

    public event Action<ClipboardEntry>? NewEntry;

    public bool IsEnabled => _isEnabled;
    public IReadOnlyList<ClipboardEntry> Entries => _entries;

    public ClipboardHistoryService()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        _historyPath = Path.Combine(dir, "clipboard_history.json");
        _logPath = Path.Combine(dir, "fairy.log");
        LoadHistory();
    }

    public void Start()
    {
        if (_isEnabled) return;
        _isEnabled = true;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(CheckIntervalMs) };
        _timer.Tick += OnTick;
        _timer.Start();
        Log("Clipboard history monitoring started");
    }

    public void Stop()
    {
        _isEnabled = false;
        _timer?.Stop();
        _timer = null;
        Log("Clipboard history monitoring stopped");
    }

    public void Clear()
    {
        _entries.Clear();
        SaveHistory();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        try
        {
            if (!Clipboard.ContainsText()) return;

            var text = Clipboard.GetText();
            if (string.IsNullOrEmpty(text) || text == _lastClipboardText) return;

            _lastClipboardText = text;

            // Skip if same as the latest entry
            if (_entries.Count > 0 && _entries[0].Text == text) return;

            var entry = new ClipboardEntry { Text = text };
            _entries.Insert(0, entry);

            while (_entries.Count > MaxEntries)
                _entries.RemoveAt(_entries.Count - 1);

            NewEntry?.Invoke(entry);
            SaveHistory();
        }
        catch (Exception ex)
        {
            Log($"Clipboard tick error: {ex.Message}");
        }
    }

    public void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
            _lastClipboardText = text;
            Log($"Copied history entry to clipboard ({text.Length} chars)");
        }
        catch (Exception ex) { Log($"Copy error: {ex.Message}"); }
    }

    private void LoadHistory()
    {
        try
        {
            if (File.Exists(_historyPath))
            {
                var json = File.ReadAllText(_historyPath);
                var saved = JsonSerializer.Deserialize<List<ClipboardEntry>>(json);
                if (saved != null) _entries.AddRange(saved);
            }
        }
        catch { }
    }

    private void SaveHistory()
    {
        try
        {
            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_historyPath, json);
        }
        catch { }
    }

    private void Log(string msg)
    {
        try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] [Clipboard] {msg}\n"); } catch { }
    }

    public void Dispose()
    {
        Stop();
    }
}
