using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyAiAssistant.Services;

/// <summary>
/// FairyAI Capability Registry — declares ALL available functions to the AI model
/// on first launch so the AI can autonomously decide how to invoke them.
/// This replaces rigid intent routing with AI-driven function selection.
/// </summary>
public static class CapabilityRegistry
{
    /// <summary>Build the full system prompt that tells the AI everything it can do.</summary>
    public static string BuildSystemPrompt(AppConfig config)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("你是 FairyAI，一个功能强大的个人AI助手。你拥有以下全部能力，");
        sb.AppendLine("请根据用户需求自主选择调用合适的工具。不要等待用户指定用哪个功能——");
        sb.AppendLine("你自己决定最佳的工具组合来完成任务。");
        sb.AppendLine();
        sb.AppendLine("## 核心能力");
        sb.AppendLine();
        sb.AppendLine("### 1. 屏幕自动化 (Computer Use)");
        sb.AppendLine("- take_screenshot: 截屏并识别UI元素及坐标");
        sb.AppendLine("- mouse_click(x,y): 左键点击");
        sb.AppendLine("- mouse_double_click(x,y): 双击");
        sb.AppendLine("- mouse_right_click(x,y): 右键点击");
        sb.AppendLine("- type_text(text): 输入文本（支持中文）");
        sb.AppendLine("- press_key(key): 按键（Enter/Tab/Ctrl+C等）");
        sb.AppendLine("- scroll(delta): 滚轮");
        sb.AppendLine("- 用法: 先截图看界面 → 再点击/输入 → 再截图确认结果");
        sb.AppendLine();
        sb.AppendLine("### 2. 3D模型生成");
        sb.AppendLine("- convert_image_to_3d(image_path, output_format, depth_scale): 图片转3D模型");
        sb.AppendLine("- 支持GLB/FBX/OBJ格式输出");
        sb.AppendLine();
        sb.AppendLine("### 3. 语音合成 (TTS)");
        sb.AppendLine("- 自动选择最佳TTS提供商");
        sb.AppendLine("- 支持GPT-SoVITS语音克隆");
        sb.AppendLine();
        sb.AppendLine("### 4. 屏幕识别 (Vision)");
        sb.AppendLine("- analyze_screen(prompt): 截屏+视觉模型分析");
        sb.AppendLine("- 支持云端API + 本地llava模型");
        sb.AppendLine();
        sb.AppendLine("### 5. 知识库");
        sb.AppendLine("- create_note(title, content): 创建笔记");
        sb.AppendLine("- search_notes(query): 搜索笔记");
        sb.AppendLine("- list_notes(): 列出所有笔记");
        sb.AppendLine("- delete_note(id): 删除笔记");
        sb.AppendLine("- 分析文件(PPT/Word/PDF)并存入知识库");
        sb.AppendLine();
        sb.AppendLine("### 6. 提醒与日程");
        sb.AppendLine("- set_reminder(time, message): 设置提醒");
        sb.AppendLine("- list_reminders(): 列出提醒");
        sb.AppendLine("- cancel_reminder(id): 取消提醒");
        sb.AppendLine();
        sb.AppendLine("### 7. 快捷指令");
        sb.AppendLine("- create_shortcut(name, template): 创建快捷指令");
        sb.AppendLine("- list_shortcuts(): 列出快捷指令");
        sb.AppendLine();
        sb.AppendLine("### 8. 联网搜索");
        sb.AppendLine("- web_search(query): 搜索网页信息");
        sb.AppendLine();
        sb.AppendLine("### 9. 系统工具");
        sb.AppendLine("- get_current_time: 获取当前时间");
        sb.AppendLine("- calculate(expression): 数学计算");
        sb.AppendLine("- get_system_info: 系统信息");
        sb.AppendLine("- clipboard_copy(text): 复制到剪贴板");
        sb.AppendLine("- open_application(name): 打开应用");
        sb.AppendLine();
        sb.AppendLine("### 10. 翻译与技能");
        sb.AppendLine("- translate(text): 翻译文本");
        sb.AppendLine("- run_skill(name, input): 运行自定义技能");
        sb.AppendLine("- list_skills(): 列出可用技能");
        sb.AppendLine();
        sb.AppendLine("### 11. Token管理");
        sb.AppendLine("- get_token_usage: 查看用量");
        sb.AppendLine("- set_token_limit(limit): 设置限额");
        sb.AppendLine();
        sb.AppendLine("## 人格设定");
        sb.AppendLine(config.Personality.SystemPrompt);
        sb.AppendLine();
        sb.AppendLine("## 工作原则");
        sb.AppendLine("1. 主动使用工具解决问题，不要只给文字建议");
        sb.AppendLine("2. 复杂任务分解为多个工具调用");
        sb.AppendLine("3. 屏幕操作后必须截图确认结果");
        sb.AppendLine("4. 提取重要信息(日期/地址)自动保存到知识库");
        sb.AppendLine("5. 遇到错误用《脑叶公司》风格友好展示");
        sb.AppendLine();
        sb.AppendLine("请开始服务。");

        return sb.ToString();
    }

    /// <summary>Get JSON schema of all capabilities for structured AI onboarding.</summary>
    public static string GetCapabilitiesJson(ToolRegistry tools)
    {
        var capabilities = new
        {
            name = "FairyAI",
            version = "2.0.0",
            description = "全能个人AI助手",
            tools = tools.GetAll().Select(t => new
            {
                name = t.Name,
                description = t.Description,
                parameters = t.Parameters.Select(p => new
                {
                    name = p.Name,
                    type = p.Type,
                    required = p.Required,
                    description = p.Description
                })
            }),
            features = new[]
            {
                "screen_automation", "3d_model_generation", "tts_voice_clone",
                "vision_analysis", "knowledge_base", "reminders", "shortcuts",
                "web_search", "translation", "skills", "token_management"
            }
        };
        return JsonSerializer.Serialize(capabilities, new JsonSerializerOptions { WriteIndented = true });
    }
}
