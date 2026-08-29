using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MyAiAssistant.Services;

/// <summary>
/// Bluetooth-style sync using RFCOMM-like TCP socket on local machine.
/// Actual Bluetooth requires WinRT APIs which are complex; this uses a
/// lightweight TCP approach for PC-to-PC LAN discovery + communication
/// as a fallback. Real Bluetooth pairing is handled via the OS Bluetooth settings.
/// </summary>
public class BluetoothSyncService : ISyncService
{
    private TcpListener? _listener;
    private readonly List<TcpClient> _clients = new();
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private readonly string _logPath;
    private readonly SyncConfig _config;

    public string ServiceName => "Bluetooth/LAN";
    public bool IsRunning => _isRunning;

    public event EventHandler<SyncMessageReceivedEventArgs>? MessageReceived;

    public BluetoothSyncService()
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
            var port = _config.LanPort;

            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _isRunning = true;

            Log($"Bluetooth/LAN sync started on port {port}");
            _ = AcceptClientsAsync(_cts.Token);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log($"Bluetooth start error: {ex.Message}");
        }
    }

    public Task StopAsync()
    {
        _cts?.Cancel();
        _isRunning = false;

        foreach (var client in _clients)
        {
            try { client.Close(); } catch { }
        }
        _clients.Clear();

        try { _listener?.Stop(); } catch { }
        _listener = null;

        Log("Bluetooth/LAN sync stopped");
        return Task.CompletedTask;
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                lock (_clients) { _clients.Add(client); }
                _ = HandleClientAsync(client, ct);
            }
            catch (OperationCanceledException) { break; }
            catch { }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        try
        {
            using var stream = client.GetStream();
            var buffer = new byte[8192];

            while (!ct.IsCancellationRequested && client.Connected)
            {
                var bytesRead = await stream.ReadAsync(buffer, ct);
                if (bytesRead == 0) break;

                var json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var msg = JsonSerializer.Deserialize<SyncMessage>(json);
                if (msg != null)
                {
                    MessageReceived?.Invoke(this, new SyncMessageReceivedEventArgs
                    {
                        FromDeviceId = msg.FromDeviceId,
                        Command = msg.Command,
                        Payload = msg.Payload
                    });
                }
            }
        }
        catch { }
        finally
        {
            lock (_clients) { _clients.Remove(client); }
            try { client.Close(); } catch { }
        }
    }

    public async Task SendCommandAsync(string command, string payload)
    {
        var msg = new SyncMessage
        {
            FromDeviceId = Environment.MachineName,
            Command = command,
            Payload = payload
        };
        var json = JsonSerializer.Serialize(msg);
        var data = Encoding.UTF8.GetBytes(json);

        List<TcpClient> snapshot;
        lock (_clients) { snapshot = _clients.ToList(); }

        foreach (var client in snapshot)
        {
            try
            {
                if (client.Connected)
                {
                    await client.GetStream().WriteAsync(data);
                }
            }
            catch { }
        }
    }

    public async Task<SyncDeviceInfo?> DiscoverDeviceAsync()
    {
        // Try to connect to known endpoints
        var config = ConfigManager.Load().Sync;
        var endpoints = new[] { $"127.0.0.1:{config.LanPort}" };

        foreach (var ep in endpoints)
        {
            try
            {
                var parts = ep.Split(':');
                if (parts.Length != 2) continue;
                var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Parse(parts[0]), int.Parse(parts[1]));
                client.Close();

                return new SyncDeviceInfo
                {
                    DeviceId = "local",
                    DeviceName = config.DeviceName,
                    Endpoint = ep,
                    Protocol = "tcp"
                };
            }
            catch { }
        }

        return null;
    }

    public void Dispose()
    {
        StopAsync().Wait();
    }

    private void Log(string msg)
    {
        try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] [BT] {msg}\n"); } catch { }
    }

    private class SyncMessage
    {
        public string FromDeviceId { get; set; } = "";
        public string Command { get; set; } = "";
        public string Payload { get; set; } = "";
    }
}
