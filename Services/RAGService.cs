using System.IO;
using System.Linq;
using MyAiAssistant.Models;

namespace MyAiAssistant.Services;

/// <summary>
/// RAG service — vector search + LLM for knowledge retrieval.
/// </summary>
public class RAGService
{
    private readonly VectorDatabase _vectorDb;
    private readonly ILlmService _llm;

    public RAGService(ILlmService llm, string knowledgeDir = "")
    {
        _llm = llm;
        var dir = string.IsNullOrEmpty(knowledgeDir)
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "knowledge")
            : knowledgeDir;
        Directory.CreateDirectory(dir);
        _vectorDb = new VectorDatabase(Path.Combine(dir, "vectordb.json"));
    }

    public async Task IndexDocumentAsync(string filePath)
    {
        var text = await File.ReadAllTextAsync(filePath);
        var chunks = SplitIntoChunks(text, 500);
        for (int i = 0; i < chunks.Count; i++)
        {
            var id = Path.GetFileNameWithoutExtension(filePath) + "_" + i;
            var embedding = await GetEmbeddingAsync(chunks[i]);
            _vectorDb.Add(id, embedding, chunks[i]);
        }
        _vectorDb.Save();
    }

    public async Task<string> QueryAsync(string question, int topK = 5)
    {
        var queryEmbedding = await GetEmbeddingAsync(question);
        var results = _vectorDb.Search(queryEmbedding, topK);
        if (results.Count == 0) return "知识库中没有找到相关信息。";

        var context = string.Join("\n\n", results.Select(r => r.Document));
        var prompt = "基于以下上下文回答问题。\n\n上下文:\n" + context + "\n\n问题: " + question;

        var config = ConfigManager.Load();
        var response = new System.Text.StringBuilder();
        await foreach (var chunk in _llm.StreamChatAsync(new List<ChatMessage>(), prompt, config.LLM.BaseUrl, config.LLM.Model, config.LLM.ApiKey))
            response.Append(chunk);
        return response.ToString();
    }

    private static List<string> SplitIntoChunks(string text, int chunkSize)
    {
        var chunks = new List<string>();
        for (int i = 0; i < text.Length; i += chunkSize)
            chunks.Add(text.Substring(i, Math.Min(chunkSize, text.Length - i)));
        return chunks;
    }

    private async Task<float[]> GetEmbeddingAsync(string text)
    {
        await Task.CompletedTask;
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));
        var embedding = new float[256];
        for (int i = 0; i < 256; i++)
            embedding[i] = hash[i % hash.Length] / 255f;
        return embedding;
    }
}
