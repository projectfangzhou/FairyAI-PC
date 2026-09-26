using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace MyAiAssistant.Services;

/// <summary>
/// Blender integration: converts images to 3D models by invoking Blender headlessly
/// with the FairyAI addon. No screen reading required.
/// </summary>
public class BlenderService
{
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fairy.log");
    private readonly string _blenderPath;

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_blenderPath) && File.Exists(_blenderPath);

    public BlenderService()
    {
        _blenderPath = FindBlender();
    }

    /// <summary>Convert an image to a 3D model using Blender headless mode.</summary>
    public async Task<(bool Success, string OutputPath, string Message)> ConvertImageTo3DAsync(
        string imagePath,
        string outputDir = "",
        string format = "GLTF",
        float depthScale = 0.3f,
        int subdivisions = 128)
    {
        if (!IsAvailable)
            return (false, "", "Blender not found. Please install Blender 3.6+.");

        if (!File.Exists(imagePath))
            return (false, "", $"Image not found: {imagePath}");

        if (string.IsNullOrWhiteSpace(outputDir))
            outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models_3d");
        Directory.CreateDirectory(outputDir);

        var ext = format.ToUpper() switch
        {
            "GLTF" or "GLB" => ".glb",
            "FBX" => ".fbx",
            "OBJ" => ".obj",
            _ => ".glb"
        };
        var outputPath = Path.Combine(outputDir,
            Path.GetFileNameWithoutExtension(imagePath) + "_3d" + ext);

        var addonScript = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "BlenderAddon", "fairyai_image_to_3d.py");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _blenderPath,
                Arguments = $"--background --python \"{addonScript}\" -- --input \"{imagePath}\" --output \"{outputPath}\" --format {format} --depth {depthScale} --subdiv {subdivisions}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            Log($"Blender: converting {Path.GetFileName(imagePath)} -> {outputPath}");

            using var process = Process.Start(psi);
            if (process == null)
                return (false, "", "Failed to start Blender process");

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Parse FAIRYAI_RESULT from stdout
            var resultLine = stdout.Split('\n')
                .FirstOrDefault(l => l.StartsWith("FAIRYAI_RESULT:"));

            if (process.ExitCode == 0 && File.Exists(outputPath))
            {
                Log($"Blender: success, output={outputPath} ({new FileInfo(outputPath).Length} bytes)");
                return (true, outputPath, "3D model created successfully");
            }

            var error = resultLine?.Split(':', 2).LastOrDefault() ?? stderr[..Math.Min(200, stderr.Length)];
            Log($"Blender: failed (exit {process.ExitCode}): {error}");
            return (false, "", $"Conversion failed: {error}");
        }
        catch (Exception ex)
        {
            Log($"Blender: exception {ex.Message}");
            return (false, "", $"Error: {ex.Message}");
        }
    }

    /// <summary>Find Blender executable.</summary>
    private static string FindBlender()
    {
        var candidates = new List<string>();

        // Check Program Files
        foreach (var drive in new[] { "C:", "D:", "E:" })
        {
            candidates.Add(Path.Combine(drive, @"Program Files\Blender Foundation\Blender 4.5\blender.exe"));
            candidates.Add(Path.Combine(drive, @"Program Files\Blender Foundation\Blender 4.2\blender.exe"));
            candidates.Add(Path.Combine(drive, @"Program Files\Blender Foundation\Blender 5.2\blender.exe"));
            candidates.Add(Path.Combine(drive, @"Program Files\Blender Foundation\Blender\blender.exe"));
            candidates.Add(Path.Combine(drive, "Steam", "steamapps", "common", "Blender", "blender.exe"));
        }

        // Check Steam
        candidates.Add(@"C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe");

        // Check PATH
        candidates.Add("blender.exe");

        // AppData local (scoop/winget)
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        candidates.Add(Path.Combine(localAppData, "Programs", "Blender", "blender.exe"));

        return candidates.FirstOrDefault(c => c == "blender.exe" || File.Exists(c)) ?? "";
    }

    /// <summary>Auto-deploy the FairyAI addon to Blender's addon directory.</summary>
    public static void DeployAddon()
    {
        var addonSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BlenderAddon", "fairyai_image_to_3d.py");
        if (!File.Exists(addonSource)) return;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var addonDirs = new[]
        {
            Path.Combine(appData, "Blender Foundation", "Blender", "4.5", "scripts", "addons"),
            Path.Combine(appData, "Blender Foundation", "Blender", "4.2", "scripts", "addons"),
            Path.Combine(appData, "Blender Foundation", "Blender", "5.2", "scripts", "addons"),
            Path.Combine(appData, "Blender Foundation", "Blender", "3.6", "scripts", "addons"),
        };

        foreach (var dir in addonDirs)
        {
            try
            {
                Directory.CreateDirectory(dir);
                var dest = Path.Combine(dir, "fairyai_image_to_3d.py");
                File.Copy(addonSource, dest, overwrite: true);
                Log($"Addon deployed to: {dest}");
            }
            catch (Exception ex)
            {
                Log($"Addon deploy to {dir} failed: {ex.Message}");
            }
        }
    }

    private static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] [BLENDER] {msg}\n"); } catch { }
    }
}
