using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MyAiAssistant.Services;

public class SkillItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Trigger { get; set; } = "";
    public string PromptBody { get; set; } = "";
    public string[] Tags { get; set; } = Array.Empty<string>();
    public DateTime LoadedAt { get; set; } = DateTime.Now;
}

public class SkillService
{
    private readonly List<SkillItem> _skills = new();
    private readonly string _skillsDir;
    private readonly string _logPath;
    public int Count => _skills.Count;

    public SkillService()
    {
        _skillsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FairyAI", "skills");
        _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
        Directory.CreateDirectory(_skillsDir);
        LoadFromDirectory(_skillsDir);
        var appDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "skills");
        if (Directory.Exists(appDir)) LoadFromDirectory(appDir);
        if (_skills.Count == 0) SeedDefaultSkills();
    }

    public List<SkillItem> ListAll() => _skills.ToList();

    public SkillItem? FindByTrigger(string trigger) =>
        _skills.FirstOrDefault(s => s.Trigger.Equals(trigger, StringComparison.OrdinalIgnoreCase) ||
                                    s.Name.Equals(trigger, StringComparison.OrdinalIgnoreCase));

    public SkillItem? FindById(string id) => _skills.FirstOrDefault(s => s.Id == id);

    public string? Execute(string skillIdentifier, string userInput)
    {
        var skill = FindByTrigger(skillIdentifier) ?? FindById(skillIdentifier);
        if (skill == null) return null;
        return skill.PromptBody.Replace("{input}", userInput);
    }

    public (bool IsSkill, string Trigger, string RemainingInput) ParseSkillCommand(string input)
    {
        var match = Regex.Match(input.TrimStart(), @"^(/[\w\-]+)\s*(.*)", RegexOptions.Singleline);
        if (!match.Success) return (false, "", input);
        var trigger = match.Groups[1].Value;
        var remaining = match.Groups[2].Value.Trim();
        var skill = FindByTrigger(trigger);
        return (skill != null, trigger, remaining);
    }

    private void LoadFromDirectory(string dir)
    {
        foreach (var file in Directory.GetFiles(dir, "*.md"))
        {
            try
            {
                var content = File.ReadAllText(file);
                var skill = ParseSkillFile(content, file);
                if (skill != null) _skills.Add(skill);
            }
            catch { }
        }
    }

    private SkillItem? ParseSkillFile(string content, string filePath)
    {
        var match = Regex.Match(content, @"^---\s*\n(.*?)\n---\s*\n(.*)", RegexOptions.Singleline);
        if (!match.Success) return null;

        var frontmatter = match.Groups[1].Value;
        var body = match.Groups[2].Value.Trim();

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in frontmatter.Split('\n'))
        {
            var colonIdx = line.IndexOf(':');
            if (colonIdx > 0)
            {
                fields[line[..colonIdx].Trim()] = line[(colonIdx + 1)..].Trim();
            }
        }

        var name = fields.TryGetValue("name", out var n) ? n : Path.GetFileNameWithoutExtension(filePath);
        var trigger = fields.TryGetValue("trigger", out var t) ? t : "";
        var desc = fields.TryGetValue("description", out var d) ? d : "";
        var tagsStr = fields.TryGetValue("tags", out var tags) ? tags : "";
        var tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new SkillItem
        {
            Id = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant(),
            Name = name, Trigger = trigger, Description = desc,
            PromptBody = body, Tags = tagList
        };
    }

    private void SeedDefaultSkills()
    {
        CreateSkillFile("summarize.md", "summarize", "/summarize", "\u603B\u7ED3\u6587\u672C\u4E3A\u8981\u70B9",
            "---\nname: \u603B\u7ED3\ntrigger: /summarize\n---\n\u8BF7\u5C06\u4EE5\u4E0B\u5185\u5BB9\u603B\u7ED3\u4E3A\u7B80\u6D01\u7684\u8981\u70B9\u5217\u8868\uFF0C\u4FDD\u7559\u5173\u952E\u4FE1\u606F\uFF1A\n\n{input}");
        CreateSkillFile("translate.md", "translate", "/translate", "\u7FFB\u8BD1\u6587\u672C\u5230\u6307\u5B9A\u8BED\u8A00",
            "---\nname: \u7FFB\u8BD1\ntrigger: /translate\n---\n\u8BF7\u5C06\u4EE5\u4E0B\u5185\u5BB9\u7FFB\u8BD1\u4E3A\u4E2D\u6587\uFF08\u5982\u679C\u5DF2\u662F\u4E2D\u6587\u5219\u7FFB\u8BD1\u4E3A\u82F1\u6587\uFF09\uFF1A\n\n{input}");
        CreateSkillFile("polish.md", "polish", "/polish", "\u6DA6\u8272\u6539\u5199\u6587\u672C",
            "---\nname: \u6DA6\u8272\ntrigger: /polish\n---\n\u8BF7\u6DA6\u8272\u6539\u5199\u4EE5\u4E0B\u6587\u672C\uFF0C\u4F7F\u5176\u66F4\u52A0\u4E13\u4E1A\u3001\u6D41\u7545\u3001\u81EA\u7136\uFF1A\n\n{input}");
        CreateSkillFile("code.md", "code", "/code", "\u4EE3\u7801\u52A9\u624B",
            "---\nname: \u4EE3\u7801\u52A9\u624B\ntrigger: /code\n---\n\u8BF7\u6839\u636E\u4EE5\u4E0B\u63CF\u8FF0\u751F\u6210\u4EE3\u7801\u6216\u89E3\u91CA\u4EE3\u7801\u903B\u8F91\uFF1A\n\n{input}\n\n\u8BF7\u7528 Markdown \u4EE3\u7801\u5757\u5305\u88F9\u4EE3\u7801\uFF0C\u5E76\u6DFB\u52A0\u7B80\u8981\u8BF4\u660E\u3002");
        CreateSkillFile("email.md", "email", "/email", "\u90AE\u4EF6\u751F\u6210",
            "---\nname: \u90AE\u4EF6\u751F\u6210\ntrigger: /email\n---\n\u8BF7\u6839\u636E\u4EE5\u4E0B\u8981\u70B9\u751F\u6210\u4E00\u5C01\u6B63\u5F0F\u7684\u4E2D\u6587\u5546\u52A1\u90AE\u4EF6\uFF08\u5305\u542B\u4E3B\u9898\u3001\u79F0\u547C\u3001\u6B63\u6587\u3001\u843D\u6B3E\uFF09\uFF1A\n\n{input}");
    }

    private void CreateSkillFile(string fileName, string id, string trigger, string desc, string body)
    {
        _skills.Add(new SkillItem
        {
            Id = id, Name = trigger.TrimStart('/'),
            Trigger = trigger, Description = desc,
            PromptBody = body, Tags = new[] { "default" }
        });
        var filePath = Path.Combine(_skillsDir, fileName);
        if (!File.Exists(filePath))
            File.WriteAllText(filePath, body, new UTF8Encoding(true));
    }

    private void Log(string msg)
    {
        try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] [Skills] {msg}\n"); } catch { }
    }
}

public class ListSkillsTool : ITool
{
    private readonly SkillService _skills;
    public string Name => "list_skills";
    public string Description => "List all available skills (slash commands)";
    public List<ToolParameter> Parameters => new();

    public ListSkillsTool(SkillService skills) => _skills = skills;

    public Task<string> ExecuteAsync(JsonObject args)
    {
        var list = _skills.ListAll();
        if (list.Count == 0) return Task.FromResult("No skills installed.");
        var sb = new StringBuilder();
        foreach (var s in list)
            sb.AppendLine($"{s.Trigger} {s.Name} - {s.Description}");
        return Task.FromResult(sb.ToString());
    }
}

public class RunSkillTool : ITool
{
    private readonly SkillService _skills;
    public string Name => "run_skill";
    public string Description => "Run a skill by trigger (e.g., /summarize) or name with user input";
    public List<ToolParameter> Parameters => new()
    {
        new ToolParameter { Name = "skill", Description = "Skill trigger or name (e.g., /summarize)" },
        new ToolParameter { Name = "input", Description = "Input text to process with the skill" }
    };

    public RunSkillTool(SkillService skills) => _skills = skills;

    public Task<string> ExecuteAsync(JsonObject args)
    {
        var skill = args["skill"]?.GetValue<string>() ?? "";
        var input = args["input"]?.GetValue<string>() ?? "";
        var prompt = _skills.Execute(skill, input);
        return Task.FromResult(prompt ?? $"Skill '{skill}' not found.");
    }
}
