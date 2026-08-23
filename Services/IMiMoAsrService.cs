namespace MyAiAssistant.Services;

public interface IMiMoAsrService
{
    Task<string> TranscribeAsync(byte[] audioData, string format = "wav");
}
