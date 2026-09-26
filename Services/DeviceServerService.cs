using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MyAiAssistant.Services;

/// <summary>
/// Device-as-server: every connected device (PC, phone, tablet) acts as a server
/// for bot connectivity and data sync. Handles PC-Mobile relay via Cloudflare Workers
/// when devices are not on the same LAN, and Mobile-Mobile direct sync.
/// </summary>
public class DeviceServerService
{
    private HttpListener? _listener;
    private readonly int _port;
    private bool _isRunning;
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public bool IsRunning => _isRunning;

    public DeviceServerService(int port = 9878)
    {
        _port = port;
    }

    /// <summary>Start local HTTP server so other devices can connect.</summary>
    public async Task<bool> StartAsync()
    {
        try
        {
            _listener = new HttpListener();
            // Security: bind to localhost only by default (not all interfaces)
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            _listener.Start();
            _isRunning = true;
            Log($"Device server started on port {_port}");

            _ = Task.Run(async () =>
            {
                while (_isRunning && _listener != null)
                {
                    try
                    {
                        var ctx = await _listener.GetContextAsync();
                        _ = Task.Run(() => HandleRequest(ctx));
                    }
                    catch { if (!_isRunning) break; }
                }
            });

            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            Log($"Device server start error: {ex.Message}");
            return false;
        }
    }

    /// <summary>Sync data to a remote device via Cloudflare Worker relay.</summary>
    public async Task<bool> SyncToRemoteAsync(string relayUrl, string deviceId, object data)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                source_device = Environment.MachineName,
                target_device = deviceId,
                data = data,
                timestamp = DateTime.UtcNow
            });

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var resp = await http.PostAsync(relayUrl,
                new StringContent(payload, Encoding.UTF8, "application/json"));
            Log($"Relay sync to {deviceId}: {resp.StatusCode}");
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log($"Relay sync error: {ex.Message}");
            return false;
        }
    }

    /// <summary>Mobile-to-Mobile sync: send data to another phone via relay.</summary>
    public async Task<bool> SyncMobileToMobileAsync(string relayUrl, string fromDevice, string toDevice, object data)
    {
        return await SyncToRemoteAsync(relayUrl, toDevice, data);
    }

    /// <summary>Get local IP address for LAN discovery.</summary>
    public string GetLocalIPAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65535);
            var endPoint = socket.LocalEndPoint as IPEndPoint;
            return endPoint?.Address.ToString() ?? "127.0.0.1";
        }
        catch { return "127.0.0.1"; }
    }

    private async Task HandleRequest(HttpListenerContext ctx)
    {
        try
        {
            var req = ctx.Request;
            var resp = ctx.Response;
            var path = req.Url?.AbsolutePath ?? "/";

            var response = path switch
            {
                "/ping" => JsonSerializer.Serialize(new { status = "ok", device = Environment.MachineName }),
                "/info" => JsonSerializer.Serialize(new
                {
                    device = Environment.MachineName,
                    platform = Environment.OSVersion.Platform.ToString(),
                    ip = GetLocalIPAddress()
                }),
                _ => JsonSerializer.Serialize(new { error = "not found" })
            };

            var buf = Encoding.UTF8.GetBytes(response);
            resp.ContentLength64 = buf.Length;
            await resp.OutputStream.WriteAsync(buf);
            resp.Close();
        }
        catch (Exception ex) { Log($"Request error: {ex.Message}"); }
    }

    public void Stop()
    {
        _isRunning = false;
        _listener?.Stop();
        _listener = null;
        Log("Device server stopped");
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [SRV] {msg}\n"); } catch { }
    }
}
