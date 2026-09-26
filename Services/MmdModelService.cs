using System.IO;

namespace MyAiAssistant.Services;

/// <summary>
/// MMD (MikuMikuDance) model support: detects and loads .pmx/.pmd model files
/// alongside Live2D models for virtual avatar display.
/// </summary>
public class MmdModelService
{
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    /// <summary>Check if a path contains a supported model (Live2D or MMD).</summary>
    public static ModelType DetectModelType(string modelPath)
    {
        if (!Directory.Exists(modelPath) && !File.Exists(modelPath))
            return ModelType.None;

        // Check for Live2D model
        if (Directory.Exists(modelPath))
        {
            if (Directory.GetFiles(modelPath, "*.model3.json").Any()
                || Directory.GetFiles(modelPath, "*.model.json").Any())
                return ModelType.Live2D;
        }

        // Check for MMD model
        var ext = Path.GetExtension(modelPath).ToLowerInvariant();
        if (ext is ".pmx" or ".pmd")
            return ModelType.MMD;

        // Check directory for MMD files
        if (Directory.Exists(modelPath))
        {
            if (Directory.GetFiles(modelPath, "*.pmx").Any()
                || Directory.GetFiles(modelPath, "*.pmd").Any())
                return ModelType.MMD;
        }

        return ModelType.None;
    }

    /// <summary>Find all MMD model files in a directory.</summary>
    public static List<string> FindMmdModels(string folder)
    {
        if (!Directory.Exists(folder)) return new();
        return Directory.GetFiles(folder, "*.pmx")
            .Concat(Directory.GetFiles(folder, "*.pmd"))
            .ToList();
    }

    /// <summary>Get MMD model info from file.</summary>
    public static MmdModelInfo GetModelInfo(string mmdPath)
    {
        return new MmdModelInfo
        {
            Path = mmdPath,
            Name = Path.GetFileNameWithoutExtension(mmdPath),
            Format = Path.GetExtension(mmdPath).ToLowerInvariant().TrimStart('.'),
            SizeBytes = new FileInfo(mmdPath).Length
        };
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [MMD] {msg}\n"); } catch { }
    }
}

public enum ModelType { None, Live2D, MMD }

public class MmdModelInfo
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public string Format { get; set; } = "";
    public long SizeBytes { get; set; }
}
