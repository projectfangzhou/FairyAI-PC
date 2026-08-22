using System.Diagnostics;
using System.IO;
using Microsoft.Data.Sqlite;

namespace MyAiAssistant.Services;

public class AppMappingService : IAppMappingService
{
    private readonly string _connStr;
    private const string DbFile = "app_mappings.db";

    // Common search paths
    private static readonly string[] SearchPaths =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps"),
        @"C:\Users\l1763\AppData\Local\Microsoft\WindowsApps",
        @"C:\Users\l1763\AppData\Roaming",
        @"C:\Users\l1763\Desktop",
        @"D:\",
    ];

    public AppMappingService()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DbFile);
        _connStr = $"Data Source={path}";
    }

    public async Task InitializeAsync()
    {
        using var conn = new SqliteConnection(_connStr);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS AppMappings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Keyword TEXT NOT NULL,
                ExePath TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_AppMappings_Keyword ON AppMappings(Keyword);
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<string?> FindAppAsync(string keyword)
    {
        using var conn = new SqliteConnection(_connStr);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ExePath FROM AppMappings WHERE Keyword = @kw LIMIT 1";
        cmd.Parameters.AddWithValue("@kw", keyword.ToLowerInvariant());
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString();
    }

    public async Task SaveMappingAsync(string keyword, string exePath)
    {
        using var conn = new SqliteConnection(_connStr);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO AppMappings (Keyword, ExePath, CreatedAt) VALUES (@kw, @path, @now)";
        cmd.Parameters.AddWithValue("@kw", keyword.ToLowerInvariant());
        cmd.Parameters.AddWithValue("@path", exePath);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    public Task<List<string>> SearchExeAsync(string keyword)
    {
        var results = new List<string>();
        var searchName = keyword.ToLowerInvariant();

        foreach (var basePath in SearchPaths)
        {
            if (!Directory.Exists(basePath)) continue;

            try
            {
                // Search for .exe and .bat files
                var files = Directory.EnumerateFiles(basePath, "*.exe", SearchOption.AllDirectories)
                    .Concat(Directory.EnumerateFiles(basePath, "*.bat", SearchOption.AllDirectories));

                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    if (fileName.Contains(searchName))
                    {
                        results.Add(file);
                        if (results.Count >= 5) return Task.FromResult(results);
                    }
                }
            }
            catch { /* skip inaccessible directories */ }
        }

        return Task.FromResult(results);
    }
}
