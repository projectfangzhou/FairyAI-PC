using Microsoft.Data.Sqlite;
using MyAiAssistant.Models;

namespace MyAiAssistant.Services;

public class ChatHistoryService : IChatHistoryService
{
    private readonly string _connStr;
    private const string DbFile = "chat_history.db";

    public ChatHistoryService()
    {
        var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DbFile);
        _connStr = $"Data Source={path}";
    }

    public async Task InitializeAsync()
    {
        using var conn = new SqliteConnection(_connStr);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Messages (
                Id TEXT PRIMARY KEY,
                SessionId TEXT NOT NULL,
                Role TEXT NOT NULL,
                Content TEXT NOT NULL,
                Timestamp TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Msg_Session ON Messages(SessionId);
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SaveAsync(string sessionId, ChatMessage message)
    {
        using var conn = new SqliteConnection(_connStr);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Messages VALUES(@Id,@Sid,@Role,@Content,@Ts)";
        cmd.Parameters.AddWithValue("@Id", message.Id);
        cmd.Parameters.AddWithValue("@Sid", sessionId);
        cmd.Parameters.AddWithValue("@Role", message.Role);
        cmd.Parameters.AddWithValue("@Content", message.Content);
        cmd.Parameters.AddWithValue("@Ts", message.Timestamp.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<ChatMessage>> LoadAsync(string sessionId)
    {
        var list = new List<ChatMessage>();
        using var conn = new SqliteConnection(_connStr);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Role,Content,Timestamp FROM Messages WHERE SessionId=@Sid ORDER BY Timestamp";
        cmd.Parameters.AddWithValue("@Sid", sessionId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ChatMessage
            {
                Id = reader.GetString(0),
                Role = reader.GetString(1),
                Content = reader.GetString(2),
                Timestamp = DateTime.Parse(reader.GetString(3))
            });
        }
        return list;
    }
}
