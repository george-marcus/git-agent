using System.Text;
using System.Text.Json;
using GitAgent.Configuration.ProviderConfigsModels;
using GitAgent.Models;
using GitAgent.Services.AI;
using GitAgent.Services.Infrastructure;

namespace GitAgent.Providers;

public class ClaudeProvider : IModelProvider
{
    private readonly ClaudeConfig _config;
    private readonly IPromptBuilder _promptBuilder;
    private readonly HttpClient _httpClient;

    public ClaudeProvider(ClaudeConfig config, IPromptBuilder promptBuilder, CachingHttpHandler cachingHandler)
    {
        _config = config;
        _promptBuilder = promptBuilder;
        _httpClient = new HttpClient(cachingHandler, disposeHandler: false)
        {
            BaseAddress = new Uri(_config.BaseUrl),
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

    public async Task<IReadOnlyList<GeneratedCommand>> GenerateGitCommands(string instruction, RepoContext context)
    {
        EnsureApiKeyConfigured();

        var userPrompt = _promptBuilder.BuildCommandUserPrompt(instruction, context);
        var request = BuildRequest(GitTools.GitCommandSystemPrompt, userPrompt, GitTools.ToolName, GitTools.ToolDescription, GitTools.GetInputSchema(), 1024);

        var result = await SendRequestAsync(request);
        LogCacheUsage(result);

        var toolInput = ExtractToolInput(result, ClaudeJsonContext.Default.GitToolInput);
        if (toolInput?.Commands != null)
        {
            return toolInput.Commands.Select(c => c.ToGeneratedCommand()).ToList();
        }

        if (result?.Content?.Any(c => c.Type == "text" && !string.IsNullOrEmpty(c.Text)) == true)
        {
            Console.Error.WriteLine("Warning: Model returned text instead of tool call");
        }

        return [];
    }

    public async Task<ConflictResolutionResult> GenerateConflictResolution(ConflictSection conflict, string filePath, string fileExtension)
    {
        EnsureApiKeyConfigured();

        var userPrompt = _promptBuilder.BuildConflictUserPrompt(conflict, filePath, fileExtension);
        var request = BuildRequest(GitTools.ConflictSystemPrompt, userPrompt, GitTools.ConflictToolName, GitTools.ConflictToolDescription, GitTools.GetConflictInputSchema(), 4096);

        var result = await SendRequestAsync(request);

        var toolInput = ExtractToolInput(result, ClaudeJsonContext.Default.ConflictToolInput);
        if (toolInput != null)
        {
            return new ConflictResolutionResult
            {
                ResolvedContent = toolInput.ResolvedContent,
                Explanation = toolInput.Explanation,
                Confidence = ParseConfidence(toolInput.Confidence)
            };
        }

        return new ConflictResolutionResult
        {
            ResolvedContent = conflict.OursContent,
            Explanation = "Failed to generate AI resolution, defaulting to 'ours'",
            Confidence = ResolutionConfidence.Low
        };
    }

    private void EnsureApiKeyConfigured()
    {
        if (string.IsNullOrWhiteSpace(_config.ApiKey))
        {
            throw new InvalidOperationException("Claude API key not configured. Run: git-agent config set claude.apiKey <your-key>");
        }
    }

    private ClaudeRequest BuildRequest(string systemPrompt, string userPrompt, string toolName, string toolDescription, JsonSchema inputSchema, int maxTokens) => new()
    {
        Model = _config.Model,
        MaxTokens = maxTokens,
        System =
        [
            new() { Type = "text", Text = systemPrompt, CacheControl = new CacheControl { Type = "ephemeral" } }
        ],
        Tools =
        [
            new() { Name = toolName, Description = toolDescription, InputSchema = inputSchema, CacheControl = new CacheControl { Type = "ephemeral" } }
        ],
        ToolChoice = new ClaudeToolChoice { Type = "tool", Name = toolName },
        Messages = [new() { Role = "user", Content = userPrompt }]
    };

    private async Task<ClaudeResponse?> SendRequestAsync(ClaudeRequest request)
    {
        var json = JsonSerializer.Serialize(request, ClaudeJsonContext.Default.ClaudeRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _config.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        _httpClient.DefaultRequestHeaders.Add("anthropic-beta", "prompt-caching-2024-07-31");

        try
        {
            var response = await _httpClient.PostAsync("/v1/messages", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Claude API error ({response.StatusCode}): {responseJson}");
            }

            return JsonSerializer.Deserialize(responseJson, ClaudeJsonContext.Default.ClaudeResponse);
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException("Claude API request timed out after 60 seconds.");
        }
    }

    private static void LogCacheUsage(ClaudeResponse? result)
    {
        if (result?.Usage == null) return;

        if (result.Usage.CacheReadInputTokens > 0)
        {
            Console.WriteLine($"(prompt cache hit: {result.Usage.CacheReadInputTokens} tokens from cache)");
        }
        else if (result.Usage.CacheCreationInputTokens > 0)
        {
            Console.WriteLine($"(prompt cache created: {result.Usage.CacheCreationInputTokens} tokens cached)");
        }
    }

    private static T? ExtractToolInput<T>(ClaudeResponse? result, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        var toolUse = result?.Content?.FirstOrDefault(c => c.Type == "tool_use");
        if (toolUse?.Input == null) return default;

        var inputJson = toolUse.Input.Value.GetRawText();
        return JsonSerializer.Deserialize(inputJson, typeInfo);
    }

    private static ResolutionConfidence ParseConfidence(string confidence) => confidence.ToLowerInvariant() switch
    {
        "high" => ResolutionConfidence.High,
        "medium" => ResolutionConfidence.Medium,
        _ => ResolutionConfidence.Low
    };
}

#region Claude API Models

internal class ClaudeRequest
{
    public string Model { get; set; } = "";
    public int MaxTokens { get; set; }
    public List<ClaudeSystemBlock>? System { get; set; }
    public List<ClaudeTool>? Tools { get; set; }
    public ClaudeToolChoice? ToolChoice { get; set; }
    public List<ClaudeMessage> Messages { get; set; } = [];
}

internal class ClaudeSystemBlock
{
    public string Type { get; set; } = "text";
    public string Text { get; set; } = "";
    public CacheControl? CacheControl { get; set; }
}

internal class CacheControl
{
    public string Type { get; set; } = "ephemeral";
}

internal class ClaudeTool
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public JsonSchema? InputSchema { get; set; }
    public CacheControl? CacheControl { get; set; }
}

internal class ClaudeToolChoice
{
    public string Type { get; set; } = "auto";
    public string? Name { get; set; }
}

internal class ClaudeMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

internal class ClaudeResponse
{
    public List<ClaudeContentBlock>? Content { get; set; }
    public ClaudeUsage? Usage { get; set; }
}

internal class ClaudeContentBlock
{
    public string? Type { get; set; }
    public string? Text { get; set; }
    public string? Id { get; set; }
    public string? Name { get; set; }
    public JsonElement? Input { get; set; }
}

internal class ClaudeUsage
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CacheCreationInputTokens { get; set; }
    public int CacheReadInputTokens { get; set; }
}

#endregion
