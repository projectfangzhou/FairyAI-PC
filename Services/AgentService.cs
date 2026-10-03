using System.Text.Json;

namespace MyAiAssistant.Services;

/// <summary>
/// Agent service with task planning, execution loop, and long-term memory.
/// Enables autonomous completion of complex multi-step tasks.
/// </summary>
public class AgentService
{
    private readonly ILlmService _llm;
    private readonly ToolRegistry _tools;
    private readonly VectorDatabase _memory;
    private readonly string _memoryPath;

    public AgentService(ILlmService llm, ToolRegistry tools)
    {
        _llm = llm;
        _tools = tools;
        _memoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agent_memory.json");
        _memory = new VectorDatabase(_memoryPath);
    }

    /// <summary>Plan and execute a complex task autonomously.</summary>
    public async Task<string> ExecuteTaskAsync(string task, int maxSteps = 10)
    {
        // Step 1: Plan the task
        var plan = await PlanTaskAsync(task);

        // Step 2: Execute steps in a loop
        var results = new List<string>();
        foreach (var step in plan.Steps)
        {
            var result = await ExecuteStepAsync(step);
            results.Add(result);

            // Store in long-term memory
            await StoreInMemoryAsync(step, result);

            // Check if task is complete
            if (result.Contains("TASK_COMPLETE") || results.Count >= maxSteps)
                break;
        }

        // Step 3: Summarize results
        return await SummarizeResultsAsync(task, results);
    }

    /// <summary>Plan a task into executable steps.</summary>
    private async Task<TaskPlan> PlanTaskAsync(string task)
    {
        var prompt = $@"将以下任务分解为可执行的步骤。每个步骤应明确使用什么工具。

任务: {task}

返回JSON格式:
{{
  ""steps"": [
    {{ ""action"": ""工具名称"", ""params"": {{}}, ""description"": ""步骤描述"" }}
  ]
}}";

        var config = ConfigManager.Load();
        var response = new System.Text.StringBuilder();
        await foreach (var chunk in _llm.StreamChatAsync([], prompt, config.LLM.BaseUrl, config.LLM.Model, config.LLM.ApiKey))
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
                {
                    foreach (var step in stepsEl.EnumerateArray())
                    {
                        steps.Add(new TaskStep
                        {
                            Action = step.GetProperty("action").GetString() ?? "",
                            Description = step.GetProperty("description").GetString() ?? ""
                        });
                    }
                }
                return new TaskPlan { Steps = steps };
            }
        }
        catch { }

        return new TaskPlan
        {
            Steps = new List<TaskStep>
            {
                new() { Action = "chat", Description = task }
            }
        };
    }

    /// <summary>Execute a single step.</summary>
    private async Task<string> ExecuteStepAsync(TaskStep step)
    {
        try
        {
            if (step.Action == "chat")
                return step.Description;

            var result = await _tools.ExecuteToolCallAsync(step.Action,
                JsonSerializer.Serialize(new { }));
            return result;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>Store step result in long-term memory.</summary>
    private async Task StoreInMemoryAsync(string action, string result)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var embedding = new float[256];
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(action + result));
        for (int i = 0; i < 256; i++)
            embedding[i] = hash[i % hash.Length] / 255f;

        _memory.Add(id, embedding, $"{action}: {result}", new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("o"),
            ["action"] = action
        });
        _memory.Save();
    }

    /// <summary>Summarize task execution results.</summary>
    private async Task<string> SummarizeResultsAsync(string task, List<string> results)
    {
        var prompt = $@"总结以下任务的执行结果。

任务: {task}
执行结果:
{string.Join("\n", results)}

请给出简洁的总结。";

        var config = ConfigManager.Load();
        var summary = new System.Text.StringBuilder();
        await foreach (var chunk in _llm.StreamChatAsync([], prompt, config.LLM.BaseUrl, config.LLM.Model, config.LLM.ApiKey))
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
