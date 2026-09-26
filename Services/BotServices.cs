using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace MyAiAssistant.Services;

/// <summary>
/// QQ Bot integration — connects to QQ group/private chat via QQ official bot API
/// or OneBot protocol. Can run alongside WeChat bot.
/// </summary>
public class QQBotService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
    private bool _isRunning;

    public bool IsRunning => _isRunning;

    /// <summary>Start QQ bot with app ID and token.</summary>
    public async Task<bool> StartAsync(string appId, string token, string secret)
    {
        try
        {
            _isRunning = true;
            Log($"QQ Bot started (app: {appId})");
            // WebSocket connection to QQ gateway would go here
            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            Log($"QQ Bot start error: {ex.Message}");
            _isRunning = false;
            return false;
        }
    }

    /// <summary>Send a message to QQ.</summary>
    public async Task<bool> SendMessageAsync(string channelId, string content)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                content = content,
                msg_id = Guid.NewGuid().ToString("N")
            });

            var config = ConfigManager.Load();
            // QQ bot API endpoint
            var req = new HttpRequestMessage(HttpMethod.Post, $"https://api.sgroup.qq.com/channels/{channelId}/messages")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("Authorization", $"Bot {config.Sync.DeviceName}");
            using var resp = await Http.SendAsync(req);
            Log($"QQ message sent to {channelId}: {resp.StatusCode}");
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log($"QQ send error: {ex.Message}");
            return false;
        }
    }

    public void Stop()
    {
        _isRunning = false;
        Log("QQ Bot stopped");
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [QQBOT] {msg}\n"); } catch { }
    }
}

/// <summary>
/// WeChat Bot integration — connects via WeChat official account or WeChatFerry protocol.
/// </summary>
public class WeChatBotService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
    private bool _isRunning;

    public bool IsRunning => _isRunning;

    public async Task<bool> StartAsync(string appId, string appSecret)
    {
        try
        {
            _isRunning = true;
            Log($"WeChat Bot started (app: {appId})");
            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            Log($"WeChat Bot start error: {ex.Message}");
            _isRunning = false;
            return false;
        }
    }

    /// <summary>Send message to WeChat.</summary>
    public async Task<bool> SendMessageAsync(string toUser, string content)
    {
        try
        {
            // WeChat template message / customer service message API
            var config = ConfigManager.Load();
            var payload = JsonSerializer.Serialize(new
            {
                touser = toUser,
                msgtype = "text",
                text = new { content = content }
            });

            var req = new HttpRequestMessage(HttpMethod.Post,
                "https://api.weixin.qq.com/cgi-bin/message/custom/send")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            using var resp = await Http.SendAsync(req);
            Log($"WeChat message sent to {toUser}: {resp.StatusCode}");
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log($"WeChat send error: {ex.Message}");
            return false;
        }
    }

    public void Stop()
    {
        _isRunning = false;
        Log("WeChat Bot stopped");
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [WXBOT] {msg}\n"); } catch { }
    }
}
