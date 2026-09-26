using System.Diagnostics;
using System.IO;

namespace MyAiAssistant.Services;

/// <summary>
/// Office suite integration — supports WPS Office and Microsoft Office
/// for document analysis (Word/Excel/PPT) and automation.
/// </summary>
public class OfficeIntegrationService
{
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public enum OfficeSuite { Unknown, WPS, MicrosoftOffice }

    /// <summary>Detect which office suite is installed.</summary>
    public OfficeSuite DetectInstalled()
    {
        // WPS paths
        var wpsPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WPS Office"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WPS Office"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kingsoft", "WPS Office"),
            @"C:\Program Files\WPS Office",
            @"C:\Program Files (x86)\WPS Office",
        };

        // MS Office paths
        var msPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Office"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Office"),
            @"C:\Program Files\Microsoft Office",
            @"C:\Program Files (x86)\Microsoft Office",
        };

        foreach (var p in wpsPaths)
            if (Directory.Exists(p)) { Log($"Detected WPS Office: {p}"); return OfficeSuite.WPS; }

        foreach (var p in msPaths)
            if (Directory.Exists(p)) { Log($"Detected Microsoft Office: {p}"); return OfficeSuite.MicrosoftOffice; }

        return OfficeSuite.Unknown;
    }

    /// <summary>Find the appropriate executable for opening a document.</summary>
    public string? FindEditor(string fileExtension)
    {
        var suite = DetectInstalled();

        return fileExtension.ToLowerInvariant() switch
        {
            ".docx" or ".doc" => suite switch
            {
                OfficeSuite.WPS => FindWpsExecutable("wps.exe"),
                OfficeSuite.MicrosoftOffice => FindMsExecutable("WINWORD.EXE"),
                _ => null
            },
            ".xlsx" or ".xls" => suite switch
            {
                OfficeSuite.WPS => FindWpsExecutable("et.exe"),
                OfficeSuite.MicrosoftOffice => FindMsExecutable("EXCEL.EXE"),
                _ => null
            },
            ".pptx" or ".ppt" => suite switch
            {
                OfficeSuite.WPS => FindWpsExecutable("wpp.exe"),
                OfficeSuite.MicrosoftOffice => FindMsExecutable("POWERPNT.EXE"),
                _ => null
            },
            _ => null
        };
    }

    /// <summary>Open a document with the detected office suite.</summary>
    public bool OpenDocument(string filePath)
    {
        if (!File.Exists(filePath)) return false;

        var ext = Path.GetExtension(filePath);
        var editor = FindEditor(ext);

        try
        {
            if (editor != null)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = editor,
                    Arguments = $"\"{filePath}\"",
                    UseShellExecute = true
                });
                Log($"Opened {Path.GetFileName(filePath)} with {Path.GetFileName(editor)}");
                return true;
            }

            // Fallback: use shell default
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
            Log($"Opened {Path.GetFileName(filePath)} with default handler");
            return true;
        }
        catch (Exception ex)
        {
            Log($"Open document error: {ex.Message}");
            return false;
        }
    }

    /// <summary>Extract text from Office document using COM or format-specific parsing.</summary>
    public async Task<string> ExtractTextAsync(string filePath)
    {
        if (!File.Exists(filePath)) return "";

        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        try
        {
            return ext switch
            {
                ".docx" => await ExtractDocxTextAsync(filePath),
                ".xlsx" => await ExtractXlsxTextAsync(filePath),
                ".pptx" => await ExtractPptxTextAsync(filePath),
                _ => await File.ReadAllTextAsync(filePath)
            };
        }
        catch (Exception ex)
        {
            Log($"Extract text error: {ex.Message}");
            return "";
        }
    }

    private static async Task<string> ExtractDocxTextAsync(string path)
    {
        using var archive = System.IO.Compression.ZipFile.OpenRead(path);
        var entry = archive.GetEntry("word/document.xml");
        if (entry == null) return "";
        using var reader = new StreamReader(entry.Open());
        var xml = await reader.ReadToEndAsync();
        return System.Text.RegularExpressions.Regex.Replace(xml, "<[^>]+>", " ").Trim();
    }

    private static async Task<string> ExtractXlsxTextAsync(string path)
    {
        using var archive = System.IO.Compression.ZipFile.OpenRead(path);
        var sb = new System.Text.StringBuilder();

        // Extract shared strings
        var sharedEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedEntry != null)
        {
            using var reader = new StreamReader(sharedEntry.Open());
            var xml = await reader.ReadToEndAsync();
            var matches = System.Text.RegularExpressions.Regex.Matches(xml, "<t[^>]*>([^<]+)</t>");
            foreach (System.Text.RegularExpressions.Match m in matches)
                sb.AppendLine(m.Groups[1].Value);
        }

        return sb.ToString();
    }

    private static async Task<string> ExtractPptxTextAsync(string path)
    {
        using var archive = System.IO.Compression.ZipFile.OpenRead(path);
        var sb = new System.Text.StringBuilder();
        foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith("ppt/slides/slide")))
        {
            using var reader = new StreamReader(entry.Open());
            var xml = await reader.ReadToEndAsync();
            var text = System.Text.RegularExpressions.Regex.Replace(xml, "<[^>]+>", " ").Trim();
            if (!string.IsNullOrWhiteSpace(text))
                sb.AppendLine(text);
        }
        return sb.ToString();
    }

    private static string? FindWpsExecutable(string exeName)
    {
        var paths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WPS Office", exeName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WPS Office", exeName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kingsoft", "WPS Office", exeName),
            @"C:\Program Files\WPS Office\" + exeName,
            @"C:\Program Files (x86)\WPS Office\" + exeName,
        };
        return paths.FirstOrDefault(File.Exists);
    }

    private static string? FindMsExecutable(string exeName)
    {
        // Check Office 16/15/14 (different version folders)
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var basePathX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        for (int version = 16; version >= 14; version--)
        {
            var paths = new[]
            {
                Path.Combine(basePath, "Microsoft Office", $"Office{version}", exeName),
                Path.Combine(basePathX86, "Microsoft Office", $"Office{version}", exeName),
                Path.Combine(basePath, "Microsoft Office", "root", $"Office{version}", exeName),
                Path.Combine(basePathX86, "Microsoft Office", "root", $"Office{version}", exeName),
            };
            var found = paths.FirstOrDefault(File.Exists);
            if (found != null) return found;
        }
        return null;
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [OFFICE] {msg}\n"); } catch { }
    }
}
