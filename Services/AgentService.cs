using System.IO;
using System.Linq;
using System.Text.Json;
using MyAiAssistant.Models;

namespace MyAiAssistant.Services;

/// <summary>
/// Agent service with task planning, execution loop, and long-term memory.
/// </summary>
public class AgentService
{
    private readonly ILlmService _llm;
    private readonly ToolRegistry _tools;
    private readonly VectorDatabase _memory;

    public AgentService(ILlmService llm, ToolRegistry tools)
    {
        _llm = llm;
        _tools = tools;
        _memory = new VectorDatabase(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agent_memory.json"));
    }

    public async Task<string> ExecuteTaskAsync(string task, int maxSteps = 10)
    {
        var plan = await PlanTaskAsync(task);
        var results = new List<string>();

        foreach (var step in plan.Steps)
        {
            var result = await ExecuteStepAsync(step);
            results.Add(result);
            await StoreInMemoryAsync(step.Description, result);
            if (result.Contains("TASK_COMPLETE") || results.Count >= maxSteps)
                break;
        }

        return await SummarizeResultsAsync(task, results);
    }

    private async Task<TaskPlan> PlanTaskAsync(string task)
    {
        var prompt = "将以下任务分解为可执行的步骤:\n" + task + "\n\n返回JSON: {\"steps\":[{\"action\":\"工具名\",\"description\":\"描述\"}]}";
        var config = ConfigManager.Load();
        var response = new System.Text.StringBuilder();
        await foreach (var chunk in _llm.StreamChatAsync(new List<ChatMessage>(), prompt, config.LLM.BaseUrl, config.LLM.Model, config.LLM.ApiKey))
            response.Append(chunk);

        try
        {
            var json = response.ToString();
            var start = json.IndexOf('{');
            var end = json.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                var doc = JsonDocument.Parse(json[start..(end + 1)]);
                var steps = new List<TaskStep>();
                if (doc.RootElement.TryGetProperty("steps", out var stepsEl))
                    foreach (var step in stepsEl.EnumerateArray())
                        steps.Add(new TaskStep
                        {
                            Action = step.GetProperty("action").GetString() ?? "",
                            Description = step.GetProperty("description").GetString() ?? ""
                        });
                return new TaskPlan { Steps = steps };
            }
        }
        catch { }

        return new TaskPlan { Steps = new List<TaskStep> { new() { Action = "chat", Description = task } } };
    }

    private async Task<string> ExecuteStepAsync(TaskStep step)
    {
        try
        {
            if (step.Action == "chat") return step.Description;
            return await _tools.ExecuteToolCallAsync(step.Action, "{}");
        }
        catch (Exception ex) { return "Error: " + ex.Message; }
    }

    private async Task StoreInMemoryAsync(string action, string result)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(action + result));
        var embedding = new float[256];
        for (int i = 0; i < 256; i++)
            embedding[i] = hash[i % hash.Length] / 255f;
        _memory.Add(id, embedding, action + ": " + result);
        _memory.Save();
        await Task.CompletedTask;
    }

    private async Task<string> SummarizeResultsAsync(string task, List<string> results)
    {
        var prompt = "总结任务执行结果:\n任务:" + task + "\n结果:\n" + string.Join("\n", results);
        var config = ConfigManager.Load();
        var summary = new System.Text.StringBuilder();
        await foreach (var chunk in _llm.StreamChatAsync(new List<ChatMessage>(), prompt, config.LLM.BaseUrl, config.LLM.Model, config.LLM.ApiKey))
            summary.Append(chunk);
        return summary.ToString();
    }
}

public class TaskPlan
{
    public List<TaskStep> Steps { get; set; } = new();
}

public class TaskStep
{
    public string Action { get; set; } = "";
    public string Description { get; set; } = "";
}
