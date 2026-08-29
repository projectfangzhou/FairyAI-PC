using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MyAiAssistant.Services;

/// <summary>
/// LAN sync and file transfer service using LocalSend-style HTTP protocol.
/// Devices announce themselves via UDP broadcast, and files/configs are
/// exchanged over a lightweight HTTP server.
/// </summary>
public class LanSyncService : ISyncService, IFileTransferService
{
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private readonly string _logPath;
    private readonly SyncConfig _config;
    private string _localEndpoint = "";

    public string ServiceName => "LAN (LocalSend)";
    public bool IsRunning => _isRunning;
    public string LocalEndpoint => _localEndpoint;

    public event EventHandler<SyncMessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<FileTransferEventArgs>? FileReceived;
    public event EventHandler<FileTransferProgressEventArgs>? ProgressChanged;

    public LanSyncService()
    {
        _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
        _config = ConfigManager.Load().Sync;
    }

    public async Task StartAsync()
    {
        if (_isRunning) return;

        try
        {
            _cts = new CancellationTokenSource();
            var port = _config.FileServerPort;

            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://+:{port}/");
            _httpListener.Start();
            _isRunning = true;
            _localEndpoint = $"http://{GetLocalIPAddress()}:{port}";

            Log($"LAN file server started on {_localEndpoint}");
            _ = ListenForRequestsAsync(_cts.Token);
            _ = BroadcastAnnouncementAsync(_cts.Token);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log($"LAN start error: {ex.Message}");
        }
    }

    public Task StopAsync()
    {
        _cts?.Cancel();
        _isRunning = false;

        try { _httpListener?.Stop(); } catch { }
        _httpListener = null;

        Log("LAN file server stopped");
        return Task.CompletedTask;
    }

    private async Task ListenForRequestsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _httpListener != null)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();
                _ = HandleRequestAsync(context, ct);
            }
            catch (OperationCanceledException) { break; }
            catch { }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            if (request.HttpMethod == "GET" && request.Url?.LocalPath == "/announce")
            {
                // Device announcement
                var info = new
                {
                    deviceId = Environment.MachineName,
                    deviceName = _config.DeviceName,
                    endpoint = _localEndpoint,
                    version = "1.3.0"
                };
                var json = JsonSerializer.Serialize(info);
                var buffer = Encoding.UTF8.GetBytes(json);
                response.ContentType = "application/json";
                await response.OutputStream.WriteAsync(buffer, ct);
            }
            else if (request.HttpMethod == "POST" && request.Url?.LocalPath == "/send")
            {
                // Receive file
                var fileName = request.Headers["X-File-Name"] ?? "unknown";
                var tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "received_files");
                Directory.CreateDirectory(tempDir);
                var tempPath = Path.Combine(tempDir, fileName);

                using (var fs = File.Create(tempPath))
                {
                    await request.InputStream.CopyToAsync(fs, ct);
                }

                var fileSize = new FileInfo(tempPath).Length;
                Log($"File received: {fileName} ({fileSize} bytes)");

                FileReceived?.Invoke(this, new FileTransferEventArgs
                {
                    FileName = fileName,
                    TempPath = tempPath,
                    Size = fileSize
                });

                var okBuffer = Encoding.UTF8.GetBytes("{\"status\":\"ok\"}");
                response.ContentType = "application/json";
                await response.OutputStream.WriteAsync(okBuffer, ct);
            }
            else if (request.HttpMethod == "POST" && request.Url?.LocalPath == "/command")
            {
                // Receive command
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                var body = await reader.ReadToEndAsync(ct);
                var msg = JsonSerializer.Deserialize<CommandMessage>(body);

                if (msg != null)
                {
                    MessageReceived?.Invoke(this, new SyncMessageReceivedEventArgs
                    {
                        FromDeviceId = msg.FromDeviceId,
                        Command = msg.Command,
                        Payload = msg.Payload
                    });
                }

                var okBuffer = Encoding.UTF8.GetBytes("{\"status\":\"ok\"}");
                response.ContentType = "application/json";
                await response.OutputStream.WriteAsync(okBuffer, ct);
            }
            else
            {
                response.StatusCode = 404;
            }
        }
        catch (Exception ex)
        {
            Log($"Request error: {ex.Message}");
            response.StatusCode = 500;
        }
        finally
        {
            response.Close();
        }
    }

    private async Task BroadcastAnnouncementAsync(CancellationToken ct)
    {
        using var udp = new UdpClient();
        udp.EnableBroadcast = true;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var info = new
                {
                    deviceId = Environment.MachineName,
                    deviceName = _config.DeviceName,
                    endpoint = _localEndpoint,
                    app = "FairyAI"
                };
                var json = JsonSerializer.Serialize(info);
                var data = Encoding.UTF8.GetBytes(json);

                // Broadcast on common ports used by LocalSend
                var broadcastEndpoints = new[]
                {
                    new IPEndPoint(IPAddress.Broadcast, 53317),
                    new IPEndPoint(IPAddress.Broadcast, 53318)
                };

                foreach (var ep in broadcastEndpoints)
                {
                    await udp.SendAsync(data, data.Length, ep);
                }

                await Task.Delay(5000, ct); // Broadcast every 5 seconds
            }
            catch (OperationCanceledException) { break; }
            catch { await Task.Delay(1000, ct); }
        }
    }

    public async Task SendCommandAsync(string command, string payload)
    {
        var msg = new CommandMessage
        {
            FromDeviceId = Environment.MachineName,
            Command = command,
            Payload = payload
        };
        var json = JsonSerializer.Serialize(msg);
        var data = new StringContent(json, Encoding.UTF8, "application/json");

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            var targetUrl = $"http://127.0.0.1:{_config.FileServerPort}/command";
            await client.PostAsync(targetUrl, data);
        }
        catch (Exception ex)
        {
            Log($"Send command error: {ex.Message}");
        }
    }

    public async Task<SyncDeviceInfo?> DiscoverDeviceAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await client.GetStringAsync($"http://127.0.0.1:{_config.FileServerPort}/announce");
            var info = JsonSerializer.Deserialize<JsonElement>(response);

            return new SyncDeviceInfo
            {
                DeviceId = info.GetProperty("deviceId").GetString() ?? "",
                DeviceName = info.GetProperty("deviceName").GetString() ?? "",
                Endpoint = info.GetProperty("endpoint").GetString() ?? "",
                Protocol = "http"
            };
        }
        catch { return null; }
    }

    public async Task<string> SendFileAsync(string filePath, string targetEndpoint)
    {
        if (!File.Exists(filePath))
            return "File not found";

        var fileName = Path.GetFileName(filePath);
        var fileBytes = await File.ReadAllBytesAsync(filePath);

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        using var content = new ByteArrayContent(fileBytes);
        content.Headers.Add("X-File-Name", fileName);
        content.Headers.Add("X-From-Device", Environment.MachineName);

        var response = await client.PostAsync($"{targetEndpoint}/send", content);
        return response.IsSuccessStatusCode ? "ok" : $"Error: {response.StatusCode}";
    }

    async Task IFileTransferService.StartServerAsync(int port)
    {
        await StartServerAsync(port);
    }

    Task IFileTransferService.StopServerAsync()
    {
        return StopAsync();
    }

    public async Task StartServerAsync(int port)
    {
        if (_isRunning) return;

        try
        {
            _cts = new CancellationTokenSource();
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://+:{port}/");
            _httpListener.Start();
            _isRunning = true;
            _localEndpoint = $"http://{GetLocalIPAddress()}:{port}";

            Log($"LAN file server started on {_localEndpoint}");
            _ = ListenForRequestsAsync(_cts.Token);
            _ = BroadcastAnnouncementAsync(_cts.Token);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log($"LAN file server start error: {ex.Message}");
        }
    }

    private static string GetLocalIPAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 53);
            return ((IPEndPoint)socket.LocalEndPoint!).Address.ToString();
        }
        catch { return "127.0.0.1"; }
    }

    public void Dispose()
    {
        StopAsync().Wait();
    }

    private void Log(string msg)
    {
        try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] [LAN] {msg}\n"); } catch { }
    }

    private class CommandMessage
    {
        public string FromDeviceId { get; set; } = "";
        public string Command { get; set; } = "";
        public string Payload { get; set; } = "";
    }
}
