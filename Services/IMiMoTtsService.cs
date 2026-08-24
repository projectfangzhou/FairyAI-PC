namespace MyAiAssistant.Services;

public interface IMiMoTtsService
{
    Task<byte[]> SynthesizeAsync(string text);
}
