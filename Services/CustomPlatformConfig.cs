namespace MyAiAssistant.Services;

/// <summary>
/// Custom/blank platform config: user provides their own BaseUrl + Model
/// for text LLM, speech TTS, and image vision — all optional independently.
/// </summary>
public class CustomPlatformConfig
{
    public bool Enabled { get; set; } = false;

    // Text LLM
    public string TextBaseUrl { get; set; } = "";
    public string TextModel { get; set; } = "";
    public string TextApiKey { get; set; } = "";

    // Speech TTS
    public string SpeechBaseUrl { get; set; } = "";
    public string SpeechModel { get; set; } = "";
    public string SpeechApiKey { get; set; } = "";

    // Vision / Image
    public string VisionBaseUrl { get; set; } = "";
    public string VisionModel { get; set; } = "";
    public string VisionApiKey { get; set; } = "";

    /// <summary>Multimodal model: one endpoint handles text + vision (no separate text/vision needed).</summary>
    public bool MultimodalEnabled { get; set; } = false;
    public string MultimodalBaseUrl { get; set; } = "";
    public string MultimodalModel { get; set; } = "";
    public string MultimodalApiKey { get; set; } = "";
}
