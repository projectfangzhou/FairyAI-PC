using System.Text.Json.Nodes;

namespace MyAiAssistant.Services;

/// <summary>
/// Function Calling tool: convert an image to a 3D model via Blender.
/// The AI calls this tool with an image path and gets back a 3D model path.
/// </summary>
public class ConvertImageTo3DTool : ITool
{
    private readonly BlenderService _blender;
    public ConvertImageTo3DTool(BlenderService blender) => _blender = blender;

    public string Name => "convert_image_to_3d";
    public string Description => "Convert an image file to a 3D model using Blender. Supports PNG/JPG input. Returns the path to the generated 3D model (GLB/FBX/OBJ).";

    public List<ToolParameter> Parameters => new()
    {
        new() { Name = "image_path", Description = "Full path to the image file (PNG/JPG)" },
        new() { Name = "output_format", Description = "Output format: GLTF (default), FBX, or OBJ", Required = false, Type = "string" },
        new() { Name = "depth_scale", Description = "Displacement depth (0.01-5.0, default 0.3)", Required = false, Type = "number" },
    };

    public async Task<string> ExecuteAsync(JsonObject args)
    {
        var imagePath = args["image_path"]?.GetValue<string>() ?? "";
        var format = args["output_format"]?.GetValue<string>() ?? "GLTF";
        var depthScale = (float)(args["depth_scale"]?.GetValue<double>() ?? 0.3);

        var (success, outputPath, message) = await _blender.ConvertImageTo3DAsync(
            imagePath, format: format, depthScale: depthScale);

        return success
            ? $"3D model created: {outputPath}"
            : $"Failed: {message}";
    }
}
