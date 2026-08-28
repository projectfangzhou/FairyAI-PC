namespace MyAiAssistant.Services;

public interface IVisionService
{
    /// <summary>Analyze a base64-encoded image with a prompt using the configured vision model.</summary>
    Task<string> AnalyzeImageAsync(string prompt, string base64Image, string? customEndpoint = null, string? customApiKey = null);

    /// <summary>Capture the primary screen to a base64-encoded PNG string.
    /// Disables system animations during capture to avoid motion artifacts.</summary>
    Task<string> CaptureScreenAsync(int quality = 75);

    /// <summary>Capture screen and analyze in one call.</summary>
    Task<string> AnalyzeScreenAsync(string prompt);

    /// <summary>Analyze image using local vision model (for offline use).</summary>
    Task<string> AnalyzeImageLocalAsync(string prompt, string base64Image, LocalVisionConfig localConfig);
}
