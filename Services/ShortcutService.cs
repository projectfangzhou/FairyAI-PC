using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyAiAssistant.Services;

public class ShortcutItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string PromptTemplate { get; set; } = "";
    public string Icon { get; set; } = "\u26A1";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class ShortcutService
{
    private readonly List<ShortcutItem> _shortcuts = new();
    private readonly string _filePath;
    private readonly string _logPath;

    public int Count => _shortcuts.Count;

    public ShortcutService()
    {
        _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shortcuts.json");
        _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
        Load();
        SeedDefaults();
    }

    public ShortcutItem Create(string name, string description, string promptTemplate, string icon = "\u26A1")
    {
        var item = new ShortcutItem { Name = name, Description = description, PromptTemplate = promptTemplate, Icon = icon };
        _shortcuts.Add(item);
        Save();
        return item;
    }

    public bool Delete(string id)
    {
        var item = _shortcuts.FirstOrDefault(s => s.Id == id);
        if (item == null) return false;
        _shortcuts.Remove(item);
        Save();
        return true;
    }

    public List<ShortcutItem> List() => _shortcuts.ToList();

    public string? Execute(string id, string userInput)
    {
        var shortcut = _shortcuts.FirstOrDefault(s => s.Id == id || s.Name == id);
        if (shortcut == null) return null;
        var prompt = shortcut.PromptTemplate.Replace("{input}", userInput);
        Log($"Shortcut '{shortcut.Name}' executed");
        return prompt;
    }

    private void SeedDefaults()
    {
        if (_shortcuts.Count > 0) return;
        Create("\u603B\u7ED3", "\u603B\u7ED3\u6587\u672C\u5185\u5BB9", "\u8BF7\u7528\u7B80\u6D01\u7684\u8981\u70B9\u603B\u7ED3\u4EE5\u4E0B\u5185\u5BB9\uFF1A\n\n{input}", "\uD83D\uDCDD");
        Create("\u7FFB\u8BD1", "\u7FFB\u8BD1\u6587\u672C", "\u8BF7\u5C06\u4EE5\u4E0B\u5185\u5BB9\u7FFB\u8BD1\u6210\u82F1\u6587\uFF08\u5982\u679C\u662F\u82F1\u6587\u5219\u7FFB\u8BD1\u6210\u4E2D\u6587\uFF09\uFF1A\n\n{input}", "\uD83C\uDF10");
        Create("\u6DA6\u8272", "\u6DA6\u8272\u6539\u5199\u6587\u672C", "\u8BF7\u6DA6\u8272\u6539\u5199\u4EE5\u4E0B\u6587\u672C\uFF0C\u4F7F\u5176\u66F4\u52A0\u4E13\u4E1A\u3001\u6D41\u7545\uFF1A\n\n{input}", "\u2728");
        Create("\u4EE3\u7801\u89E3\u91CA", "\u89E3\u91CA\u4EE3\u7801", "\u8BF7\u89E3\u91CA\u4EE5\u4E0B\u4EE3\u7801\u7684\u529F\u80FD\u548C\u903B\u8F91\uFF1A\n\n{input}", "\uD83D\uDCBB");
        Create("\u90AE\u4EF6", "\u751F\u6210\u90AE\u4EF6", "\u8BF7\u6839\u636E\u4EE5\u4E0B\u8981\u70B9\u751F\u6210\u4E00\u5C01\u6B63\u5F0F\u7684\u5546\u52A1\u90AE\u4EF6\uFF1A\n\n{input}", "\uD83D\uDCE7");
        Save();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var saved = JsonSerializer.Deserialize<List<ShortcutItem>>(json);
                if (saved != null) _shortcuts.AddRange(saved);
            }
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_shortcuts, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch { }
    }

    private void Log(string msg)
    {
        try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] [Shortcut] {msg}\n"); } catch { }
    }
}

public class CreateShortcutTool : ITool
{
    private readonly ShortcutService _shortcuts;
    public string Name => "create_shortcut";
    public string Description => "Create a reusable shortcut with a prompt template. Use {input} as placeholder for user input.";
    public List<ToolParameter> Parameters => new()
    {
        new ToolParameter { Name = "name", Description = "Shortcut name" },
        new ToolParameter { Name = "description", Description = "What the shortcut does" },
        new ToolParameter { Name = "prompt_template", Description = "Prompt template with {input} placeholder" },
        new ToolParameter { Name = "icon", Description = "Emoji icon (optional)", Required = false }
    };

    public CreateShortcutTool(ShortcutService shortcuts) => _shortcuts = shortcuts;

    public Task<string> ExecuteAsync(JsonObject args)
    {
        var name = args["name"]?.GetValue<string>() ?? "";
        var desc = args["description"]?.GetValue<string>() ?? "";
        var template = args["prompt_template"]?.GetValue<string>() ?? "";
        var icon = args["icon"]?.GetValue<string>() ?? "\u26A1";
        var item = _shortcuts.Create(name, desc, template, icon);
        return Task.FromResult($"Shortcut created: {item.Icon} {item.Name} [{item.Id}]");
    }
}

public class ListShortcutsTool : ITool
{
    private readonly ShortcutService _shortcuts;
    public string Name => "list_shortcuts";
    public string Description => "List all available shortcuts";
    public List<ToolParameter> Parameters => new();

    public ListShortcutsTool(ShortcutService shortcuts) => _shortcuts = shortcuts;

    public Task<string> ExecuteAsync(JsonObject args)
    {
        var list = _shortcuts.List();
        if (list.Count == 0) return Task.FromResult("No shortcuts configured.");
        var sb = new StringBuilder();
        foreach (var s in list)
            sb.AppendLine($"{s.Icon} {s.Name} - {s.Description} [{s.Id}]");
        return Task.FromResult(sb.ToString());
    }
}
