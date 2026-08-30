using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Threading;

namespace MyAiAssistant.Services;

public class ReminderItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime TriggerAt { get; set; }
    public bool IsTriggered { get; set; }
}

/// <summary>
/// Manages scheduled reminders. Checks periodically and shows popup when due.
/// Persists to JSON file.
/// </summary>
public class ReminderService
{
    private readonly DispatcherTimer _timer;
    private readonly List<ReminderItem> _reminders = new();
    private readonly string _filePath;
    private readonly string _logPath;
    private readonly Action<string, string>? _onTriggered; // (title, message) callback

    public int PendingCount => _reminders.Count(r => !r.IsTriggered);

    public event Action<ReminderItem>? ReminderFired;

    public ReminderService(Action<string, string>? onTriggered = null)
    {
        _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reminders.json");
        _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
        _onTriggered = onTriggered;

        Load();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += (_, _) => CheckReminders();
    }

    public void Start() { _timer.Start(); CheckReminders(); }
    public void Stop() => _timer.Stop();

    public ReminderItem AddReminder(string title, string message, DateTime triggerAt)
    {
        var item = new ReminderItem { Title = title, Message = message, TriggerAt = triggerAt };
        _reminders.Add(item);
        Save();
        Log($"Reminder set: {title} at {triggerAt:yyyy-MM-dd HH:mm:ss}");
        return item;
    }

    public bool CancelReminder(string id)
    {
        var item = _reminders.FirstOrDefault(r => r.Id == id);
        if (item == null) return false;
        _reminders.Remove(item);
        Save();
        Log($"Reminder cancelled: {item.Title}");
        return true;
    }

    public List<ReminderItem> GetAllReminders() => _reminders.ToList();
    public List<ReminderItem> GetPendingReminders() => _reminders.Where(r => !r.IsTriggered).ToList();

    private void CheckReminders()
    {
        var now = DateTime.Now;
        var due = _reminders.Where(r => !r.IsTriggered && r.TriggerAt <= now).ToList();

        foreach (var item in due)
        {
            item.IsTriggered = true;
            Save();
            Log($"Reminder fired: {item.Title}");
            ReminderFired?.Invoke(item);
            _onTriggered?.Invoke($"鎻愰啋: {item.Title}", item.Message);
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var saved = JsonSerializer.Deserialize<List<ReminderItem>>(json);
                if (saved != null) _reminders.AddRange(saved);
            }
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_reminders, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch { }
    }

    private void Log(string msg)
    {
        try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] [Reminder] {msg}\n"); } catch { }
    }
}

// ===== Tool implementations =====

public class SetReminderTool : ITool
{
    private readonly ReminderService _reminders;
    public string Name => "set_reminder";
    public string Description => "Set a timed reminder. The reminder will pop up at the specified time.";
    public List<ToolParameter> Parameters => new()
    {
        new ToolParameter { Name = "title", Description = "Short title for the reminder" },
        new ToolParameter { Name = "message", Description = "Full message to display", Required = false },
        new ToolParameter { Name = "minutes_from_now", Description = "Number of minutes from now to trigger", Type = "number" }
    };

    public SetReminderTool(ReminderService reminders) => _reminders = reminders;

    public Task<string> ExecuteAsync(JsonObject args)
    {
        var title = args["title"]?.GetValue<string>() ?? "鎻愰啋";
        var message = args["message"]?.GetValue<string>() ?? title;
        var minutes = args["minutes_from_now"]?.GetValue<double>() ?? 5;

        var triggerAt = DateTime.Now.AddMinutes(minutes);
        var item = _reminders.AddReminder(title, message, triggerAt);

        return Task.FromResult($"Reminder set for {triggerAt:HH:mm:ss} (in {minutes} min): {title}");
    }
}

public class ListRemindersTool : ITool
{
    private readonly ReminderService _reminders;
    public string Name => "list_reminders";
    public string Description => "List all pending reminders";
    public List<ToolParameter> Parameters => new();

    public ListRemindersTool(ReminderService reminders) => _reminders = reminders;

    public Task<string> ExecuteAsync(JsonObject args)
    {
        var pending = _reminders.GetPendingReminders();
        if (pending.Count == 0) return Task.FromResult("No pending reminders.");

        var lines = pending.Select(r => $"- [{r.Id}] {r.Title} 鈥?{r.TriggerAt:yyyy-MM-dd HH:mm}");
        return Task.FromResult(string.Join("\n", lines));
    }
}

public class CancelReminderTool : ITool
{
    private readonly ReminderService _reminders;
    public string Name => "cancel_reminder";
    public string Description => "Cancel a pending reminder by ID";
    public List<ToolParameter> Parameters => new()
    {
        new ToolParameter { Name = "id", Description = "The reminder ID to cancel" }
    };

    public CancelReminderTool(ReminderService reminders) => _reminders = reminders;

    public Task<string> ExecuteAsync(JsonObject args)
    {
        var id = args["id"]?.GetValue<string>() ?? "";
        var ok = _reminders.CancelReminder(id);
        return Task.FromResult(ok ? $"Reminder {id} cancelled." : $"Reminder {id} not found.");
    }
}
