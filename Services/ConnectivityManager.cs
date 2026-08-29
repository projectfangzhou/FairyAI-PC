using System.IO;

namespace MyAiAssistant.Services;

/// <summary>
/// Orchestrates all connectivity services (Bluetooth/LAN, SignalR, NAT traversal).
/// Provides a unified interface for device sync and file transfer.
/// </summary>
public class ConnectivityManager : IDisposable
{
    private readonly BluetoothSyncService _bluetooth;
    private readonly LanSyncService _lan;
    private readonly SignalRSyncService _signalR;
    private readonly NatTraversalService _natTraversal;
    private readonly SyncConfig _config;
    private readonly string _logPath;

    public BluetoothSyncService Bluetooth => _bluetooth;
    public LanSyncService Lan => _lan;
    public SignalRSyncService SignalR => _signalR;
    public NatTraversalService NatTraversal => _natTraversal;

    public bool IsAnyRunning => _bluetooth.IsRunning || _lan.IsRunning || _signalR.IsRunning;

    public event EventHandler<SyncMessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<FileTransferEventArgs>? FileReceived;

    public ConnectivityManager()
    {
        _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
        _config = ConfigManager.Load().Sync;

        _bluetooth = new BluetoothSyncService();
        _lan = new LanSyncService();
        _signalR = new SignalRSyncService();
        _natTraversal = new NatTraversalService();

        // Wire up events
        _bluetooth.MessageReceived += (_, e) => MessageReceived?.Invoke(this, e);
        _lan.MessageReceived += (_, e) => MessageReceived?.Invoke(this, e);
        _signalR.MessageReceived += (_, e) => MessageReceived?.Invoke(this, e);
        _lan.FileReceived += (_, e) => FileReceived?.Invoke(this, e);
    }

    public async Task StartAllAsync()
    {
        if (!_config.Enabled)
        {
            Log("Sync disabled in config");
            return;
        }

        if (_config.EnableBluetooth)
        {
            await _bluetooth.StartAsync();
            Log("Bluetooth sync started");
        }

        if (_config.EnableLan)
        {
            await _lan.StartServerAsync(_config.FileServerPort);
            await _lan.StartAsync();
            Log("LAN sync started");

            // Start NAT traversal if configured
            var natConfig = ConfigManager.Load().NatTraversal;
            if (natConfig.Enabled)
            {
                await _natTraversal.StartAsync(_config.FileServerPort);
            }
        }

        if (_config.EnableSignalR && !string.IsNullOrWhiteSpace(_config.SignalRUrl))
        {
            await _signalR.StartAsync();
            Log("SignalR sync started");
        }
    }

    public async Task StopAllAsync()
    {
        await _bluetooth.StopAsync();
        await _lan.StopAsync();
        await _signalR.StopAsync();
        _natTraversal.Stop();
        Log("All sync services stopped");
    }

    public async Task SendCommandAsync(string command, string payload)
    {
        if (_bluetooth.IsRunning)
            await _bluetooth.SendCommandAsync(command, payload);
        if (_lan.IsRunning)
            await _lan.SendCommandAsync(command, payload);
        if (_signalR.IsRunning)
            await _signalR.SendCommandAsync(command, payload);
    }

    public async Task<string> SendFileAsync(string filePath)
    {
        if (_lan.IsRunning)
        {
            var device = await _lan.DiscoverDeviceAsync();
            if (device != null)
                return await _lan.SendFileAsync(filePath, device.Endpoint);
        }
        return "No device available";
    }

    public void Dispose()
    {
        StopAllAsync().Wait();
        _bluetooth.Dispose();
        _lan.Dispose();
        _signalR.Dispose();
        _natTraversal.Dispose();
    }

    private void Log(string msg)
    {
        try { File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss}] [Sync] {msg}\n"); } catch { }
    }
}
