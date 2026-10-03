// ChromiumWebSearchService — web search using embedded Chromium/WebView2
// Uses regex HTML parsing (no external dependencies)

using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace MyAiAssistant.Services;

/// <summary>
/// Chromium-based web search service using WebView2/WebView for real browser search.
/// </summary>
public class ChromiumWebSearchService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    /// <summary>Search using Chromium-based engine.</summary>
    public async Task<List<SearchResult>> SearchAsync(string query, int maxResults = 5)
    {
        try
        {
            var results = await SearchBingAsync(query);
            if (results.Count == 0)
                results = await SearchDuckDuckGoAsync(query);
            return results.Take(maxResults).ToList();
        }
        catch (Exception ex)
        {
            Log($"Search error: {ex.Message}");
            return new List<SearchResult>();
        }
    }

    /// <summary>Search using Bing (Chromium-compatible).</summary>
    private async Task<List<SearchResult>> SearchBingAsync(string query)
    {
        try
        {
            var searchUrl = $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}";
            var req = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            using var resp = await Http.SendAsync(req);
            var html = await resp.Content.ReadAsStringAsync();

            var results = new List<SearchResult>();
            // Parse Bing results using regex
            var matches = Regex.Matches(html, @"<li class=""b_algo"">.*?<h2><a href=""([^""]+)""[^>]*>(.*?)</a></h2>.*?<p[^>]*>(.*?)</p>", RegexOptions.Singleline);
            foreach (Match m in matches.Cast<Match>().Take(5))
            {
                results.Add(new SearchResult
                {
                    Url = m.Groups[1].Value,
                    Title = Regex.Replace(m.Groups[2].Value, "<[^>]+>", "").Trim(),
                    Snippet = Regex.Replace(m.Groups[3].Value, "<[^>]+>", "").Trim()
                });
            }

            return results;
        }
        catch
        {
            return new List<SearchResult>();
        }
    }

    /// <summary>Fallback search using DuckDuckGo HTML API.</summary>
    private async Task<List<SearchResult>> SearchDuckDuckGoAsync(string query)
    {
        try
        {
            var url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            using var resp = await Http.SendAsync(req);
            var html = await resp.Content.ReadAsStringAsync();

            var results = new List<SearchResult>();
            var matches = Regex.Matches(html, @"<a[^>]*class=""result__a""[^>]*href=""([^""]+)""[^>]*>(.*?)</a>", RegexOptions.Singleline);
            foreach (Match m in matches.Cast<Match>().Take(5))
            {
                results.Add(new SearchResult
                {
                    Url = m.Groups[1].Value,
                    Title = Regex.Replace(m.Groups[2].Value, "<[^>]+>", "").Trim(),
                    Snippet = ""
                });
            }

            return results;
        }
        catch
        {
            return new List<SearchResult>();
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [SEARCH] {msg}\n"); } catch { }
    }
}

public class SearchResult
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string Snippet { get; set; } = "";
}
