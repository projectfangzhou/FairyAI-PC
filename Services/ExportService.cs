using System.IO;
using System.Text;
using Microsoft.Win32;
using MyAiAssistant.Models;

namespace MyAiAssistant.Services;

/// <summary>
/// Exports conversation history to Markdown, TXT, or PDF (text-only).
/// </summary>
public static class ExportService
{
    /// <summary>Export to file via save dialog. Returns the file path if saved, null if cancelled.</summary>
    public static string? ExportWithDialog(List<ChatMessage> messages)
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出对话",
            Filter = "Markdown 文件 (*.md)|*.md|纯文本 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            FileName = $"FairyAI_对话_{DateTime.Now:yyyyMMdd_HHmmss}",
            DefaultExt = ".md"
        };

        if (dialog.ShowDialog() != true) return null;

        var path = dialog.FileName;
        var ext = Path.GetExtension(path).ToLowerInvariant();

        var content = ext == ".txt"
            ? FormatAsText(messages)
            : FormatAsMarkdown(messages);

        File.WriteAllText(path, content, new UTF8Encoding(true));
        return path;
    }

    public static string FormatAsMarkdown(List<ChatMessage> messages)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Fairy AI 对话记录");
        sb.AppendLine();
        sb.AppendLine($"> 导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var msg in messages)
        {
            var role = msg.Role switch
            {
                "user" => "**用户**",
                "assistant" => "**Fairy AI**",
                _ => $"**{msg.Role}**"
            };
            sb.AppendLine($"{role}  ");
            sb.AppendLine($"{msg.Timestamp:HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine(msg.Content);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public static string FormatAsText(List<ChatMessage> messages)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Fairy AI 对话记录");
        sb.AppendLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(new string('=', 50));
        sb.AppendLine();

        foreach (var msg in messages)
        {
            var role = msg.Role switch
            {
                "user" => "用户",
                "assistant" => "AI",
                _ => msg.Role
            };
            sb.AppendLine($"[{msg.Timestamp:HH:mm:ss}] {role}:");
            sb.AppendLine(msg.Content);
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
