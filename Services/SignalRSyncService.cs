using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace MyAiAssistant.Services;

/// <summary>
/// SignalR-based sync service for remote device communication.
/// Connects to a SignalR hub for real-time messaging and file transfer coordination.
/// </summary>
public class SignalRSyncService : ISyncService
{
    private HubConnection? _connection;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private readonly string _logPath;
    private readonly SyncConfig _config;

    public string ServiceName => "SignalR";
    public bool IsRunning => _isRunning;

    public event EventHandler<SyncMessageReceivedEventArgs>? MessageReceived;

    public SignalRSyncService()
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

            _connection = new HubConnectionBuilder()
                .WithUrl(_config.SignalRUrl)
                .WithAutomaticReconnect()
                .Build();

            // Listen for messages from other devices
            _connection.On<string, string, string>("ReceiveCommand", (fromDevice, command, payload) =>
            {
                Log($"Received command from {fromDevice}: {command}");
                MessageReceived?.Invoke(this, new SyncMessageReceivedEventArgs
                {
                    FromDeviceId = fromDevice,
                    Command = command,
                    Payload = payload
                });
            });

            _connection.On<string, string>("ReceiveFileNotification", (fromDevice, fileInfo) =>
            {
                Log($"File notification from {fromDevice}: {fileInfo}");
            });

            _connection.Reconnecting += error =>
            {
                Log($"SignalR reconnecting: {error?.Message}");
                return Task.CompletedTask;
            };

            _connection.Reconnected += _ =>
            {
                Log("SignalR reconnected");
                return Task.CompletedTask;
            };

            await _connection.StartAsync(_cts.Token);
            _isRunning = true;

            // Register this device
            await _connection.InvokeAsync("RegisterDevice",
                Environment.MachineName, _config.DeviceName, _cts.Token);

            Log($"SignalR connected to {_config.SignalRUrl}");
        }
        catch (Exception ex)
        {
            Log($"SignalR start error: {ex.Message}");
        }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        _isRunning = false;

        if (_connection != null)
        {
            try
            {
                await _connection.InvokeAsync("UnregisterDevice", Environment.MachineName);
                await _connection.StopAsync();
            }
            catch { }
            try { await _connection.DisposeAsync(); } catch { }
            _connection = null;
        }

        Log("SignalR disconnected");
    }

    public async Task SendCommandAsync(string command, string payload)
    {
        if (_connection?.State != HubConnectionState.Connected)
        {
            Log("SignalR not connected, cannot send command");
            return;
        }

        try
        {
            await _connection.InvokeAsync("SendCommand",
                Environment.MachineName, command, payload);
        }
        catch (Exception ex)
        {
            Log($"SignalR send error: {ex.Message}");
        }
    }

    public async Task<SyncDeviceInfo?> DiscoverDeviceAsync()
    {
        if (_connection?.State != HubConnectionState.Connected)
            return null;

        try
        {
            var devices = await _connection.InvokeAsync<List<DeviceInfo>>("GetOnlineDevices");
            var firstDevice = devices.FirstOrDefault(d => d.DeviceId != Environment.MachineName);
            if (firstDevice == null) return null;

            return new SyncDeviceInfo
            {
                DeviceId = firstDevice.DeviceId,
                DeviceName = firstDevice.DeviceName,
                Endpoint = firstDevice.Endpoint,
                Protocol = "signalr"
            };
        }
        catch (Exception ex)
        {
            Log($"SignalR discover error: {ex.Message}");
            return null;
        }
    }

    public async Task SendFileAsync(string filePath, string targetDeviceId)
    {
        if (_connection?.State != HubConnectionState.Connected || !File.Exists(filePath))
            return;

        try
        {
            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var base64 = Convert.ToBase64String(fileBytes);

            await _connection.InvokeAsync("SendFile",
                Environment.MachineName, targetDeviceId,
                Path.GetFileName(filePath), base64);
        }
        catch (Exception ex)
        {
            Log($"SignalR file send error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        StopAsync().Wait();
    }

    private void Log(string msg)
    {
        try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] [SignalR] {msg}\n"); } catch { }
    }

    private class DeviceInfo
    {
        public string DeviceId { get; set; } = "";
        public string DeviceName { get; set; } = "";
        public string Endpoint { get; set; } = "";
    }
}
