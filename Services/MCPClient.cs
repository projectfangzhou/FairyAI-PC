using System.Text.Json;

namespace MyAiAssistant.Services;

/// <summary>
/// MCP (Model Context Protocol) client — connects to standardized tool ecosystem
/// including filesystem, GitHub, database, browser automation.
/// </summary>
public class MCPClient
{
    private readonly HttpClient _http;
    private readonly string _mcpEndpoint;

    public MCPClient(string endpoint = "")
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _mcpEndpoint = string.IsNullOrEmpty(endpoint)
            ? "http://localhost:3000/mcp"
            : endpoint;
    }

    /// <summary>List available MCP tools.</summary>
    public async Task<List<MCPTool>> ListToolsAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{_mcpEndpoint}/tools");
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var tools = new List<MCPTool>();

            if (doc.RootElement.TryGetProperty("tools", out var toolsEl))
            {
                foreach (var tool in toolsEl.EnumerateArray())
                {
                    tools.Add(new MCPTool
                    {
                        Name = tool.GetProperty("name").GetString() ?? "",
                        Description = tool.GetProperty("description").GetString() ?? "",
                        Parameters = tool.GetProperty("parameters").GetRawText()
                    });
                }
            }
            return tools;
        }
        catch
        {
            return new List<MCPTool>();
        }
    }

    /// <summary>Call an MCP tool.</summary>
    public async Task<string> CallToolAsync(string toolName, string parameters)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                tool = toolName,
                parameters = JsonSerializer.Deserialize<object>(parameters)
            });

            var response = await _http.PostAsync($"{_mcpEndpoint}/call",
                new StringContent(payload, Encoding.UTF8, "application/json"));
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            return $"MCP error: {ex.Message}";
        }
    }

    /// <summary>Connect to filesystem MCP server.</summary>
    public async Task<bool> ConnectFilesystemAsync(string rootPath)
    {
        return await ConnectAsync("filesystem", new { root = rootPath });
    }

    /// <summary>Connect to GitHub MCP server.</summary>
    public async Task<bool> ConnectGitHubAsync(string token)
    {
        return await ConnectAsync("github", new { token });
    }

    /// <summary>Connect to database MCP server.</summary>
    public async Task<bool> ConnectDatabaseAsync(string connectionString)
    {
        return await ConnectAsync("database", new { connection = connectionString });
    }

    /// <summary>Connect to browser automation MCP server.</summary>
    public async Task<bool> ConnectBrowserAsync()
    {
        return await ConnectAsync("browser", new { });
    }

    private async Task<bool> ConnectAsync(string serverType, object config)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { type = serverType, config });
            var response = await _http.PostAsync($"{_mcpEndpoint}/connect",
                new StringContent(payload, Encoding.UTF8, "application/json"));
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

public class MCPTool
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Parameters { get; set; } = "{}";
}
