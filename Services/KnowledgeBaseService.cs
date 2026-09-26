using System.IO;
using System.Text.Json;

namespace MyAiAssistant.Services;

/// <summary>
/// Local knowledge base: auto-save important info (dates, addresses) and
/// analyze user-dropped files (PPT/Word/PDF). Integrates with Obsidian vault.
/// </summary>
public class KnowledgeBaseService
{
    private readonly string _kbFolder;
    private readonly string _obsidianVault;
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public KnowledgeBaseService()
    {
        _kbFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "knowledge");
        _obsidianVault = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "FairyAI-Knowledge");
        Directory.CreateDirectory(_kbFolder);
        Directory.CreateDirectory(_obsidianVault);
    }

    public string KbFolder => _kbFolder;
    public string ObsidianVault => _obsidianVault;

    /// <summary>Auto-save important extracted info (dates, addresses, etc).</summary>
    public async Task SaveImportantInfoAsync(string category, string content)
    {
        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var filePath = Path.Combine(_kbFolder, $"{category}_{date}.md");
        var entry = $"## {DateTime.Now:HH:mm}\n{content}\n\n";
        await File.AppendAllTextAsync(filePath, entry);
        // Also sync to Obsidian
        var obsidianPath = Path.Combine(_obsidianVault, $"{category}_{date}.md");
        await File.AppendAllTextAsync(obsidianPath, entry);
        Log($"Saved important info [{category}]: {content[..Math.Min(60, content.Length)]}...");
    }

    /// <summary>Extract knowledge from a document file (PPT/Word/PDF) into the KB.</summary>
    public async Task<string> AnalyzeDocumentAsync(string filePath)
    {
        if (!File.Exists(filePath)) return "文件不存在";

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        string text = ext switch
        {
            ".txt" or ".md" => await File.ReadAllTextAsync(filePath),
            ".docx" or ".doc" => await ExtractDocxTextAsync(filePath),
            ".pptx" or ".ppt" => await ExtractPptxTextAsync(filePath),
            ".pdf" => await ExtractPdfTextAsync(filePath),
            _ => await File.ReadAllTextAsync(filePath),
        };

        if (string.IsNullOrWhiteSpace(text))
            return $"无法从 {ext} 文件提取文本内容";

        // Save to KB
        var kbFile = Path.Combine(_kbFolder, Path.GetFileNameWithoutExtension(filePath) + ".md");
        await File.WriteAllTextAsync(kbFile, $"# {Path.GetFileName(filePath)}\n\n{text}");

        // Save to Obsidian
        var obsFile = Path.Combine(_obsidianVault, Path.GetFileNameWithoutExtension(filePath) + ".md");
        await File.WriteAllTextAsync(obsFile, $"# {Path.GetFileName(filePath)}\n\n{text}");

        Log($"Document analyzed: {Path.GetFileName(filePath)} ({text.Length} chars)");
        return text.Length > 2000 ? text[..2000] + "..." : text;
    }

    /// <summary>List all knowledge files.</summary>
    public List<string> ListKnowledge()
    {
        return Directory.GetFiles(_kbFolder, "*.md")
            .Select(f => Path.GetFileName(f))
            .OrderBy(f => f)
            .ToList();
    }

    /// <summary>Search knowledge base.</summary>
    public List<string> Search(string query)
    {
        var results = new List<string>();
        foreach (var file in Directory.GetFiles(_kbFolder, "*.md"))
        {
            var content = File.ReadAllText(file);
            if (content.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                var idx = content.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                var start = Math.Max(0, idx - 50);
                var excerpt = content.Substring(start, Math.Min(200, content.Length - start));
                results.Add($"[{Path.GetFileName(file)}] {excerpt}");
            }
        }
        return results;
    }

    private static async Task<string> ExtractDocxTextAsync(string path)
    {
        try
        {
            // Simple ZIP-based extraction for .docx
            using var archive = System.IO.Compression.ZipFile.OpenRead(path);
            var entry = archive.GetEntry("word/document.xml");
            if (entry == null) return "";
            using var reader = new StreamReader(entry.Open());
            var xml = await reader.ReadToEndAsync();
            // Strip XML tags
            return System.Text.RegularExpressions.Regex.Replace(xml, "<[^>]+>", " ").Trim();
        }
        catch { return ""; }
    }

    private static async Task<string> ExtractPptxTextAsync(string path)
    {
        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(path);
            var sb = new System.Text.StringBuilder();
            foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("ppt/slides/slide")))
            {
                using var reader = new StreamReader(entry.Open());
                var xml = await reader.ReadToEndAsync();
                var text = System.Text.RegularExpressions.Regex.Replace(xml, "<[^>]+>", " ").Trim();
                sb.AppendLine(text);
            }
            return sb.ToString();
        }
        catch { return ""; }
    }

    private static async Task<string> ExtractPdfTextAsync(string path)
    {
        // Basic PDF text extraction (raw stream text)
        try
        {
            var bytes = await File.ReadAllBytesAsync(path);
            var text = System.Text.Encoding.Latin1.GetString(bytes);
            // Extract text between BT and ET markers (basic PDF text operators)
            var matches = System.Text.RegularExpressions.Regex.Matches(text, "\\(([^)]+)\\)\\s*Tj");
            var sb = new System.Text.StringBuilder();
            foreach (System.Text.RegularExpressions.Match m in matches)
                sb.Append(m.Groups[1].Value).Append(' ');
            return sb.ToString();
        }
        catch { return ""; }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [KB] {msg}\n"); } catch { }
    }
}
