using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyAiAssistant.Services;

public class TokenUsageRecord
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Model { get; set; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens => InputTokens + OutputTokens;
}

public class TokenLimitConfig
{
    public int DailyLimit { get; set; } = 0;
    public int MonthlyLimit { get; set; } = 0;
    public bool AlertOnLimit { get; set; } = true;
}

public class TokenUsageService
{
    private readonly List<TokenUsageRecord> _records = new();
    private readonly string _filePath;
    private readonly string _logPath;
    private TokenLimitConfig _limitConfig = new();
    private bool _limitAlertedToday;
    public event Action<string, string>? LimitAlert;

    public TokenUsageService()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        _filePath = Path.Combine(dir, "token_usage.json");
        _logPath = Path.Combine(dir, "fairy.log");
        Load();
    }

    public void Record(string model, int inputTokens, int outputTokens)
    {
        _records.Add(new TokenUsageRecord { Model = model, InputTokens = inputTokens, OutputTokens = outputTokens });
        Save();
        if (_limitConfig.AlertOnLimit && !_limitAlertedToday) CheckLimits();
    }

    public void SetLimits(int dailyLimit, int monthlyLimit)
    {
        _limitConfig.DailyLimit = dailyLimit;
        _limitConfig.MonthlyLimit = monthlyLimit;
        _limitConfig.AlertOnLimit = true;
        Save();
    }

    public (int DailyUsed, int MonthlyUsed, int DailyLimit, int MonthlyLimit) GetUsageStats()
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var dailyUsed = _records.Where(r => r.Timestamp.Date == today).Sum(r => r.TotalTokens);
        var monthlyUsed = _records.Where(r => r.Timestamp >= monthStart).Sum(r => r.TotalTokens);
        return (dailyUsed, monthlyUsed, _limitConfig.DailyLimit, _limitConfig.MonthlyLimit);
    }

    public string GetUsageReport()
    {
        var (dailyUsed, monthlyUsed, dailyLimit, monthlyLimit) = GetUsageStats();
        var today = DateTime.Today;
        var totalRecords = _records.Count(r => r.Timestamp.Date == today);
        var sb = new StringBuilder();
        sb.AppendLine("Token Usage Statistics");
        sb.AppendLine($"Date: {today:yyyy-MM-dd}");
        sb.AppendLine($"Today calls: {totalRecords}");
        sb.AppendLine($"Today tokens: {dailyUsed:N0}" + (dailyLimit > 0 ? $" / {dailyLimit:N0}" : " (unlimited)"));
        sb.AppendLine($"This month: {monthlyUsed:N0}" + (monthlyLimit > 0 ? $" / {monthlyLimit:N0}" : " (unlimited)"));
        var recent = _records.Where(r => r.Timestamp.Date == today).TakeLast(5).ToList();
        if (recent.Count > 0)
        {
            sb.AppendLine("Recent calls:");
            foreach (var r in recent)
                sb.AppendLine($"  {r.Timestamp:HH:mm} | {r.Model} | {r.InputTokens}+{r.OutputTokens}={r.TotalTokens}");
        }
        return sb.ToString();
    }

    private void CheckLimits()
    {
        var (dailyUsed, monthlyUsed, dailyLimit, monthlyLimit) = GetUsageStats();
        if (dailyLimit > 0 && dailyUsed >= dailyLimit)
        {
            _limitAlertedToday = true;
            LimitAlert?.Invoke("Token Limit Warning", $"Daily token limit reached: {dailyUsed:N0} / {dailyLimit:N0}");
        }
        else if (monthlyLimit > 0 && monthlyUsed >= monthlyLimit)
        {
            _limitAlertedToday = true;
            LimitAlert?.Invoke("Token Limit Warning", $"Monthly token limit reached: {monthlyUsed:N0} / {monthlyLimit:N0}");
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("records", out var recordsEl))
                foreach (var r in recordsEl.EnumerateArray())
                    _records.Add(new TokenUsageRecord
                    {
                        Timestamp = r.GetProperty("Timestamp").GetDateTime(),
                        Model = r.TryGetProperty("Model", out var m) ? m.GetString() ?? "" : "",
                        InputTokens = r.TryGetProperty("InputTokens", out var i) ? i.GetInt32() : 0,
                        OutputTokens = r.TryGetProperty("OutputTokens", out var o) ? o.GetInt32() : 0
                    });
            if (root.TryGetProperty("limits", out var limitsEl))
                _limitConfig = new TokenLimitConfig
                {
                    DailyLimit = limitsEl.TryGetProperty("dailyLimit", out var dl) ? dl.GetInt32() : 0,
                    MonthlyLimit = limitsEl.TryGetProperty("monthlyLimit", out var ml) ? ml.GetInt32() : 0,
                    AlertOnLimit = limitsEl.TryGetProperty("alertOnLimit", out var al) ? al.GetBoolean() : true
                };
        }
        catch { }
    }

    private void Save()
    {
        try { File.WriteAllText(_filePath, JsonSerializer.Serialize(new { records = _records, limits = _limitConfig }, new JsonSerializerOptions { WriteIndented = true })); } catch { }
    }

    private void Log(string msg) { try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] [Token] {msg}\n"); } catch { } }
}

public class GetTokenUsageTool : ITool
{
    private readonly TokenUsageService _usage;
    public string Name => "get_token_usage";
    public string Description => "Get token usage statistics";
    public List<ToolParameter> Parameters => new();
    public GetTokenUsageTool(TokenUsageService usage) => _usage = usage;
    public Task<string> ExecuteAsync(JsonObject args) => Task.FromResult(_usage.GetUsageReport());
}

public class SetTokenLimitTool : ITool
{
    private readonly TokenUsageService _usage;
    public string Name => "set_token_limit";
    public string Description => "Set daily or monthly token limits (0=unlimited)";
    public List<ToolParameter> Parameters => new()
    {
        new() { Name = "daily_limit", Description = "Max tokens per day", Type = "number", Required = false },
        new() { Name = "monthly_limit", Description = "Max tokens per month", Type = "number", Required = false }
    };
    public SetTokenLimitTool(TokenUsageService usage) => _usage = usage;
    public Task<string> ExecuteAsync(JsonObject args)
    {
        var daily = (int)(args["daily_limit"]?.GetValue<double>() ?? 0);
        var monthly = (int)(args["monthly_limit"]?.GetValue<double>() ?? 0);
        _usage.SetLimits(daily, monthly);
        return Task.FromResult($"Limits set: daily={daily}, monthly={monthly}");
    }
}
