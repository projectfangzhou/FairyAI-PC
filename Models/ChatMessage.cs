namespace MyAiAssistant.Models;

public class ChatMessage
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required string Role { get; init; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public List<string>? FileResults { get; init; }
}
