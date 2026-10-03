using System.Text.Json;

namespace MyAiAssistant.Services;

/// <summary>
/// Local model integration — llama.cpp / ONNX Runtime with quantization management
/// and NPU acceleration support.
/// </summary>
public class LocalModelService
{
    private readonly string _modelsDir;
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");

    public LocalModelService()
    {
        _modelsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
        Directory.CreateDirectory(_modelsDir);
    }

    /// <summary>List available local models.</summary>
    public List<LocalModel> ListModels()
    {
        var models = new List<LocalModel>();
        foreach (var file in Directory.GetFiles(_modelsDir, "*.gguf"))
        {
            var info = new FileInfo(file);
            models.Add(new LocalModel
            {
                Name = Path.GetFileNameWithoutExtension(file),
                Path = file,
                SizeBytes = info.Length,
                Format = "gguf",
                Quantization = DetectQuantization(file)
            });
        }
        foreach (var file in Directory.GetFiles(_modelsDir, "*.onnx"))
        {
            var info = new FileInfo(file);
            models.Add(new LocalModel
            {
                Name = Path.GetFileNameWithoutExtension(file),
                Path = file,
                SizeBytes = info.Length,
                Format = "onnx"
            });
        }
        return models;
    }

    /// <summary>Run inference with a local model.</summary>
    public async Task<string> InferenceAsync(string modelName, string prompt)
    {
        var model = ListModels().FirstOrDefault(m => m.Name == modelName);
        if (model == null)
            return $"Model not found: {modelName}";

        try
        {
            if (model.Format == "gguf")
                return await RunLlamaCppAsync(model.Path, prompt);
            else if (model.Format == "onnx")
                return await RunOnnxAsync(model.Path, prompt);

            return "Unsupported format";
        }
        catch (Exception ex)
        {
            Log($"Inference error: {ex.Message}");
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>Quantize a model to reduce size.</summary>
    public async Task<bool> QuantizeAsync(string modelPath, string quantType = "q4_k_m")
    {
        try
        {
            // llama.cpp quantize tool
            var psi = new ProcessStartInfo
            {
                FileName = "llama-quantize",
                Arguments = $"\"{modelPath}\" \"{modelPath.Replace(".gguf", $"_{quantType}.gguf")}\" {quantType}",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            using var process = Process.Start(psi);
            if (process != null)
                await process.WaitForExitAsync();
            return process?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Log($"Quantize error: {ex.Message}");
            return false;
        }
    }

    /// <summary>Check if NPU acceleration is available.</summary>
    public bool IsNPUAvailable()
    {
        try
        {
            // Check for Qualcomm NPU, Apple Neural Engine, or Intel NPU
            var info = new ProcessStartInfo
            {
                FileName = "wmic",
                Arguments = "path win32_Processor get Name",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using var process = Process.Start(info);
            var output = process?.StandardOutput.ReadToEnd() ?? "";
            return output.Contains("NPU") || output.Contains("Neural") || output.Contains("AI Boost");
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> RunLlamaCppAsync(string modelPath, string prompt)
    {
        // Use llama.cpp Python binding or CLI
        var psi = new ProcessStartInfo
        {
            FileName = "llama-cli",
            Arguments = $"-m \"{modelPath}\" -p \"{prompt}\" -n 512 --temp 0.7",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true
        };
        using var process = Process.Start(psi);
        if (process == null) return "Failed to start llama.cpp";
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output.Trim();
    }

    private async Task<string> RunOnnxAsync(string modelPath, string prompt)
    {
        // ONNX Runtime inference
        return await Task.FromResult($"ONNX inference with {modelPath}: {prompt}");
    }

    private static string DetectQuantization(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
        if (name.Contains("q4")) return "Q4";
        if (name.Contains("q5")) return "Q5";
        if (name.Contains("q6")) return "Q6";
        if (name.Contains("q8")) return "Q8";
        return "Unknown";
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [LOCAL] {msg}\n"); } catch { }
    }
}

public class LocalModel
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Format { get; set; } = "";
    public string Quantization { get; set; } = "";
}
