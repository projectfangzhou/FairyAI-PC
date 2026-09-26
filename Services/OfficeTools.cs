using System.Text.Json.Nodes;

namespace MyAiAssistant.Services;

/// <summary>
/// Function Calling tool: open and analyze Office documents (WPS/Microsoft Office).
/// </summary>
public class OpenOfficeDocTool : ITool
{
    private readonly OfficeIntegrationService _office;
    private readonly KnowledgeBaseService _kb;

    public OpenOfficeDocTool(OfficeIntegrationService office, KnowledgeBaseService kb)
    {
        _office = office;
        _kb = kb;
    }

    public string Name => "open_office_document";
    public string Description => "Open a Word/Excel/PPT document (WPS or Microsoft Office). Returns extracted text content and saves to knowledge base.";

    public List<ToolParameter> Parameters => new()
    {
        new() { Name = "file_path", Description = "Full path to the document (.docx/.xlsx/.pptx/.doc/.xls/.ppt)" },
    };

    public async Task<string> ExecuteAsync(JsonObject args)
    {
        var filePath = args["file_path"]?.GetValue<string>() ?? "";

        if (!System.IO.File.Exists(filePath))
            return $"File not found: {filePath}";

        // Extract text content
        var text = await _office.ExtractTextAsync(filePath);

        // Save to knowledge base
        if (!string.IsNullOrWhiteSpace(text))
        {
            await _kb.AnalyzeDocumentAsync(filePath);
        }

        // Open with office suite
        var opened = _office.OpenDocument(filePath);
        var suite = _office.DetectInstalled();

        return $"已打开: {System.IO.Path.GetFileName(filePath)} ({suite})\n" +
               $"内容摘要: {(text.Length > 500 ? text[..500] + "..." : text)}\n" +
               $"已存入知识库";
    }
}

/// <summary>
/// Function Calling tool: get Office suite installation info.
/// </summary>
public class GetOfficeInfoTool : ITool
{
    private readonly OfficeIntegrationService _office;
    public GetOfficeInfoTool(OfficeIntegrationService office) => _office = office;

    public string Name => "get_office_info";
    public string Description => "Get installed Office suite info (WPS or Microsoft Office) and supported formats.";

    public List<ToolParameter> Parameters => new();

    public Task<string> ExecuteAsync(JsonObject args)
    {
        var suite = _office.DetectInstalled();
        var info = suite switch
        {
            OfficeIntegrationService.OfficeSuite.WPS => "WPS Office 已安装\n支持: .docx .doc .xlsx .xls .pptx .ppt",
            OfficeIntegrationService.OfficeSuite.MicrosoftOffice => "Microsoft Office 已安装\n支持: .docx .doc .xlsx .xls .pptx .ppt",
            _ => "未检测到Office套件\n可打开 .docx .xlsx .pptx (通过系统默认程序)"
        };
        return Task.FromResult(info);
    }
}
