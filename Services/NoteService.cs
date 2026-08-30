using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyAiAssistant.Services;

public class NoteItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string[] Tags { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Simple note-taking service with search. Stores notes in JSON file.
/// AI can create, search, list, and delete notes.
/// </summary>
public class NoteService
{
    private readonly List<NoteItem> _notes = new();
    private readonly string _filePath;
    private readonly string _logPath;

    public int Count => _notes.Count;

    public NoteService()
    {
        _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "notes.json");
        _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
        Load();
    }

    public NoteItem Create(string title, string content, string[] tags)
    {
        var note = new NoteItem
        {
            Title = title,
            Content = content,
            Tags = tags,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        _notes.Add(note);
        Save();
        Log($"Note created: {note.Title} [{note.Id}]");
        return note;
    }

    public NoteItem? Get(string id) =>
        _notes.FirstOrDefault(n => n.Id == id);

    public List<NoteItem> List(int limit = 20) =>
        _notes.OrderByDescending(n => n.UpdatedAt).Take(limit).ToList();

    public List<NoteItem> Search(string query)
    {
        var q = query.ToLowerInvariant();
        return _notes
            .Where(n => n.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        n.Content.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        n.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(n => n.UpdatedAt)
            .ToList();
    }

    public bool Delete(string id)
    {
        var note = _notes.FirstOrDefault(n => n.Id == id);
        if (note == null) return false;
        _notes.Remove(note);
        Save();
        Log($"Note deleted: {note.Title} [{note.Id}]");
        return true;
    }

    public NoteItem? Update(string id, string? title, string? content, string[]? tags)
    {
        var note = _notes.FirstOrDefault(n => n.Id == id);
        if (note == null) return null;

        if (title != null) note.Title = title;
        if (content != null) note.Content = content;
        if (tags != null) note.Tags = tags;
        note.UpdatedAt = DateTime.Now;
        Save();
        Log($"Note updated: {note.Title} [{note.Id}]");
        return note;
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var saved = JsonSerializer.Deserialize<List<NoteItem>>(json);
                if (saved != null) _notes.AddRange(saved);
            }
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_notes, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch { }
    }

    private void Log(string msg)
    {
        try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] [Notes] {msg}\n"); } catch { }
    }
}

// ===== Tool implementations =====

public class CreateNoteTool : ITool
{
    private readonly NoteService _notes;
    public string Name => "create_note";
    public string Description => "Create a new note in the knowledge base. Use this when the user wants to save information for later reference.";
    public List<ToolParameter> Parameters => new()
    {
        new ToolParameter { Name = "title", Description = "Note title" },
        new ToolParameter { Name = "content", Description = "Note content" },
        new ToolParameter { Name = "tags", Description = "Comma-separated tags", Required = false }
    };

    public CreateNoteTool(NoteService notes) => _notes = notes;

    public Task<string> ExecuteAsync(JsonObject args)
    {
        var title = args["title"]?.GetValue<string>() ?? "Untitled";
        var content = args["content"]?.GetValue<string>() ?? "";
        var tagsStr = args["tags"]?.GetValue<string>() ?? "";
        var tags = tagsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var note = _notes.Create(title, content, tags);
        return Task.FromResult($"Note created: [{note.Id}] {note.Title}");
    }
}

public class SearchNotesTool : ITool
{
    private readonly NoteService _notes;
    public string Name => "search_notes";
    public string Description => "Search notes by keyword in title, content, or tags";
    public List<ToolParameter> Parameters => new()
    {
        new ToolParameter { Name = "query", Description = "Search keyword" }
    };

    public SearchNotesTool(NoteService notes) => _notes = notes;

    public Task<string> ExecuteAsync(JsonObject args)
    {
        var query = args["query"]?.GetValue<string>() ?? "";
        var results = _notes.Search(query);

        if (results.Count == 0) return Task.FromResult("No notes found.");

        var sb = new StringBuilder();
        foreach (var n in results.Take(10))
        {
            sb.AppendLine($"[{n.Id}] {n.Title}");
            sb.AppendLine($"  Tags: {string.Join(", ", n.Tags)}");
            sb.AppendLine($"  {n.Content[..Math.Min(200, n.Content.Length)]}");
            sb.AppendLine();
        }
        return Task.FromResult(sb.ToString());
    }
}

public class ListNotesTool : ITool
{
    private readonly NoteService _notes;
    public string Name => "list_notes";
    public string Description => "List recent notes (most recent first)";
    public List<ToolParameter> Parameters => new()
    {
        new ToolParameter { Name = "limit", Description = "Max notes to return (default 10)", Type = "number", Required = false }
    };

    public ListNotesTool(NoteService notes) => _notes = notes;

    public Task<string> ExecuteAsync(JsonObject args)
    {
        var limit = (int)(args["limit"]?.GetValue<double>() ?? 10);
        var notes = _notes.List(limit);

        if (notes.Count == 0) return Task.FromResult("No notes.");

        var sb = new StringBuilder();
        foreach (var n in notes)
            sb.AppendLine($"[{n.Id}] {n.Title} 鈥?{n.UpdatedAt:MM-dd HH:mm}");
        return Task.FromResult(sb.ToString());
    }
}

public class DeleteNoteTool : ITool
{
    private readonly NoteService _notes;
    public string Name => "delete_note";
    public string Description => "Delete a note by ID";
    public List<ToolParameter> Parameters => new()
    {
        new ToolParameter { Name = "id", Description = "Note ID" }
    };

    public DeleteNoteTool(NoteService notes) => _notes = notes;

    public Task<string> ExecuteAsync(JsonObject args)
    {
        var id = args["id"]?.GetValue<string>() ?? "";
        var ok = _notes.Delete(id);
        return Task.FromResult(ok ? $"Note {id} deleted." : $"Note {id} not found.");
    }
}
