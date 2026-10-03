using System.Linq;
using System.IO;
using System.Text.Json;

namespace MyAiAssistant.Services;

/// <summary>
/// Vector database for RAG semantic retrieval.
/// Stores document embeddings and enables similarity search.
/// </summary>
public class VectorDatabase
{
    private readonly string _dbPath;
    private readonly Dictionary<string, float[]> _vectors = new();
    private readonly Dictionary<string, string> _documents = new();
    private readonly Dictionary<string, Dictionary<string, object>> _metadata = new();

    public VectorDatabase(string dbPath = "")
    {
        _dbPath = string.IsNullOrEmpty(dbPath)
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vectordb.json")
            : dbPath;
        Load();
    }

    /// <summary>Add a document with its embedding vector.</summary>
    public void Add(string id, float[] embedding, string document, Dictionary<string, object>? metadata = null)
    {
        _vectors[id] = embedding;
        _documents[id] = document;
        if (metadata != null) _metadata[id] = metadata;
    }

    /// <summary>Search for similar documents by query embedding.</summary>
    public List<(string Id, string Document, float Score, Dictionary<string, object>? Metadata)> Search(
        float[] queryEmbedding, int topK = 5, float minScore = 0.0f)
    {
        var results = new List<(string, string, float, Dictionary<string, object>?)>();

        foreach (var (id, embedding) in _vectors)
        {
            float score = CosineSimilarity(queryEmbedding, embedding);
            if (score >= minScore)
                results.Add((id, _documents[id], score,
                    _metadata.TryGetValue(id, out var m) ? m : null));
        }

        return results
            .OrderByDescending(r => r.Item3)
            .Take(topK)
            .ToList();
    }

    /// <summary>Remove a document by ID.</summary>
    public bool Remove(string id)
    {
        _vectors.Remove(id);
        _documents.Remove(id);
        _metadata.Remove(id);
        return true;
    }

    /// <summary>Get document count.</summary>
    public int Count => _vectors.Count;

    /// <summary>Save database to disk.</summary>
    public void Save()
    {
        var data = new
        {
            vectors = _vectors,
            documents = _documents,
            metadata = _metadata
        };
        File.WriteAllText(_dbPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Load database from disk.</summary>
    private void Load()
    {
        try
        {
            if (!File.Exists(_dbPath)) return;
            var json = File.ReadAllText(_dbPath);
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("vectors", out var vectors))
            {
                foreach (var prop in vectors.EnumerateObject())
                {
                    var arr = prop.Value.EnumerateArray().Select(x => (float)x.GetDouble()).ToArray();
                    _vectors[prop.Name] = arr;
                }
            }
            if (doc.RootElement.TryGetProperty("documents", out var docs))
            {
                foreach (var prop in docs.EnumerateObject())
                    _documents[prop.Name] = prop.Value.GetString() ?? "";
            }
            if (doc.RootElement.TryGetProperty("metadata", out var meta))
            {
                foreach (var prop in meta.EnumerateObject())
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var m in prop.Value.EnumerateObject())
                        dict[m.Name] = m.Value.ToString() ?? "";
                    _metadata[prop.Name] = dict;
                }
            }
        }
        catch { }
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return dot / (float)(Math.Sqrt(normA) * Math.Sqrt(normB) + 1e-8);
    }
}

