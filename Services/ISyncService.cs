namespace MyAiAssistant.Services;

public interface ISyncService : IDisposable
{
    string ServiceName { get; }
    bool IsRunning { get; }
    Task StartAsync();
    Task StopAsync();
    Task SendCommandAsync(string command, string payload);
    Task<SyncDeviceInfo?> DiscoverDeviceAsync();

    event EventHandler<SyncMessageReceivedEventArgs>? MessageReceived;
}

public class SyncDeviceInfo
{
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string Protocol { get; set; } = "";
}

public class SyncMessageReceivedEventArgs : EventArgs
{
    public string FromDeviceId { get; set; } = "";
    public string Command { get; set; } = "";
    public string Payload { get; set; } = "";
}

public interface IFileTransferService : IDisposable
{
    bool IsRunning { get; }
    Task StartServerAsync(int port);
    Task StopServerAsync();
    Task<string> SendFileAsync(string filePath, string targetEndpoint);
    string LocalEndpoint { get; }

    event EventHandler<FileTransferEventArgs>? FileReceived;
    event EventHandler<FileTransferProgressEventArgs>? ProgressChanged;
}

public class FileTransferEventArgs : EventArgs
{
    public string FileName { get; set; } = "";
    public string TempPath { get; set; } = "";
    public long Size { get; set; }
}

public class FileTransferProgressEventArgs : EventArgs
{
    public string FileName { get; set; } = "";
    public long BytesTransferred { get; set; }
    public long TotalBytes { get; set; }
}
