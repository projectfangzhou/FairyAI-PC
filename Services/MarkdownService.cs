using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;

namespace MyAiAssistant.Services;

/// <summary>
/// Creates the Markdig pipeline and provides syntax-highlighted code rendering.
/// </summary>
public static class MarkdownService
{
    private static MarkdownPipeline? _pipeline;

    public static MarkdownPipeline Pipeline => _pipeline ??= CreatePipeline();

    private static MarkdownPipeline CreatePipeline()
    {
        return new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
    }
}

/// <summary>
/// Simple syntax highlighter for code blocks — wraps text in colored Runs.
/// </summary>
public static class SyntaxHighlighter
{
    private static readonly Dictionary<string, HashSet<string>> LanguageKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["csharp"] = new(new[] { "public", "private", "class", "static", "void", "var", "new", "return", "if", "else", "for", "foreach", "while", "using", "namespace", "async", "await", "Task", "int", "string", "bool", "true", "false", "null", "this", "readonly", "override", "virtual", "interface", "enum", "const", "try", "catch", "finally", "throw", "string", "object", "decimal" }),
        ["cs"] = null!,
        ["python"] = new(new[] { "def", "class", "import", "from", "return", "if", "elif", "else", "for", "while", "try", "except", "finally", "with", "as", "lambda", "None", "True", "False", "self", "async", "await", "print", "in", "is", "not", "and", "or", "raise", "yield" }),
        ["javascript"] = new(new[] { "function", "const", "let", "var", "return", "if", "else", "for", "while", "class", "import", "export", "from", "async", "await", "new", "this", "null", "undefined", "true", "false", "typeof", "instanceof", "switch", "case", "break", "try", "catch", "finally", "throw" }),
        ["typescript"] = null!,
        ["json"] = new(new[] { "true", "false", "null" }),
        ["bash"] = new(new[] { "if", "then", "else", "fi", "for", "while", "do", "done", "function", "return", "echo", "cd", "ls", "export", "set", "test", "sudo", "apt", "npm", "pip", "dotnet", "git", "mkdir", "rm", "cp", "mv" }),
        ["shell"] = null!,
        ["powershell"] = new(new[] { "if", "else", "foreach", "for", "while", "function", "param", "return", "$true", "$false", "$null", "Write-Host", "Get-", "Set-", "New-", "Remove-", "Import-", "Select-" }),
        ["xml"] = new(new[] { "xmlns", "version", "encoding" }),
        ["html"] = null!,
        ["xaml"] = null!,
        ["sql"] = new(new[] { "SELECT", "FROM", "WHERE", "INSERT", "UPDATE", "DELETE", "CREATE", "TABLE", "DROP", "ALTER", "JOIN", "ON", "AND", "OR", "NOT", "NULL", "INT", "VARCHAR", "PRIMARY", "KEY", "INDEX" }),
    };

    static SyntaxHighlighter()
    {
        LanguageKeywords["cs"] = LanguageKeywords["csharp"];
        LanguageKeywords["typescript"] = LanguageKeywords["javascript"];
        LanguageKeywords["shell"] = LanguageKeywords["bash"];
        LanguageKeywords["html"] = LanguageKeywords["xml"];
        LanguageKeywords["xaml"] = LanguageKeywords["xml"];
    }

    /// <summary>
    /// Produce colored Inlines for a code string with a given language tag.
    /// </summary>
    public static List<Inline> Colorize(string code, string language)
    {
        var output = new List<Inline>();
        var keywords = LanguageKeywords.TryGetValue(language, out var kw) ? kw : null;

        // Tokenize: strings, comments, keywords, numbers, rest
        var pattern = @"(\/\/[^\n]*|#[^\n]*|\*\*[^*]+\*\*)"  // line / hash comments
                    + @"|(""([^""\\]|\\.)*""|'([^'\\]|\\.)*')" // strings
                    + @"|(\b[a-zA-Z_]\w*\b)"                    // identifiers
                    + @"|(\b\d+\.?\d*\b)";                      // numbers
        var matches = Regex.Matches(code, pattern, RegexOptions.Singleline);

        var pos = 0;
        foreach (Match m in matches)
        {
            // Plain text before this match
            if (m.Index > pos)
            {
                output.Add(new Run(code[pos..m.Index]) { Foreground = Brush(Colors.LightGray) });
            }
            pos = m.Index + m.Length;

            var text = m.Value;

            // Comment
            if (text.StartsWith("//") || text.StartsWith("#"))
            {
                output.Add(new Run(text) { Foreground = Brush(Color.FromRgb(0x6a, 0x99, 0x55)) });
            }
            // String
            else if (text.StartsWith('"') || text.StartsWith('\''))
            {
                output.Add(new Run(text) { Foreground = Brush(Color.FromRgb(0xce, 0x91, 0x78)) });
            }
            // Keyword
            else if (keywords != null && keywords.Contains(text))
            {
                output.Add(new Run(text) { Foreground = Brush(Color.FromRgb(0x56, 0x94, 0xf0)) });
            }
            // Number
            else if (char.IsDigit(text[0]))
            {
                output.Add(new Run(text) { Foreground = Brush(Color.FromRgb(0xb5, 0xce, 0xa8)) });
            }
            else
            {
                output.Add(new Run(text) { Foreground = Brush(Colors.LightGray) });
            }
        }

        if (pos < code.Length)
            output.Add(new Run(code[pos..]) { Foreground = Brush(Colors.LightGray) });

        return output;
    }

    /// <summary>
    /// Build a WPF Border containing a highlighted code block.
    /// </summary>
    public static UIElement BuildCodeBlock(string code, string language)
    {
        var textBlock = new TextBlock
        {
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Code, Consolas, Courier New"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 0)
        };
        textBlock.Inlines.AddRange(Colorize(code, language));

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, 123, 104, 238)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 4, 0, 4),
            Child = textBlock
        };
    }

    private static SolidColorBrush Brush(Color c) => new(c);
}
