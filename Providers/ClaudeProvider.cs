using System.Text;
using System.Text.Json;
using GitAgent.Configuration;
using GitAgent.Models;
using GitAgent.Services;

namespace GitAgent.Providers;

public class ClaudeProvider : IModelProvider
{
    private readonly ClaudeConfig _config;
    private readonly IPromptBuilder _promptBuilder;
    private readonly HttpClient _httpClient;

    private const string SystemPrompt = """
        You are a git command generator. Your task is to translate natural language instructions into git commands.

        Rules:
        - Use the execute_git_commands tool to return the commands
        - Prefer safe operations (status, add, commit, push, branch, checkout, fetch, pull)
        - Mark destructive commands (reset --hard, clean -fd, force push, branch -D) with risk 'destructive'
        - Mark commands that modify state (commit, push, merge) with risk 'moderate'
        - Mark read-only commands (status, log, diff, branch --list) with risk 'safe'
        - Return commands in the order they should be executed
        """;

    public ClaudeProvider(ClaudeConfig config, IPromptBuilder promptBuilder, IResponseParser responseParser, CachingHttpHandler cachingHandler)
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
        if (string.IsNullOrWhiteSpace(_config.ApiKey))
        {
            throw new InvalidOperationException("Claude API key not configured. Run: git-agent config set claude.apiKey <your-key>");
        }

        var prompt = _promptBuilder.BuildPrompt(instruction, context);

        var requestBody = new ClaudeRequest
        {
            Model = _config.Model,
            MaxTokens = 1024,
            System = new List<ClaudeSystemBlock>
            {
                new()
                {
                    Type = "text",
                    Text = SystemPrompt,
                    CacheControl = new CacheControl { Type = "ephemeral" }
                }
            },
            Tools = new List<ClaudeTool>
            {
                new()
                {
                    Name = GitTools.ToolName,
                    Description = GitTools.ToolDescription,
                    InputSchema = GitTools.GetInputSchema(),
                    CacheControl = new CacheControl { Type = "ephemeral" }
                }
            },
            ToolChoice = new ClaudeToolChoice { Type = "tool", Name = GitTools.ToolName },
            Messages = new List<ClaudeMessage>
            {
                new()
                {
                    Role = "user",
                    Content = prompt
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody, ClaudeJsonContext.Default.ClaudeRequest);
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

            var result = JsonSerializer.Deserialize(responseJson, ClaudeJsonContext.Default.ClaudeResponse);

            if (result?.Usage != null)
            {
                if (result.Usage.CacheReadInputTokens > 0)
                {
                    Console.WriteLine($"(prompt cache hit: {result.Usage.CacheReadInputTokens} tokens from cache)");
                }
                else if (result.Usage.CacheCreationInputTokens > 0)
                {
                    Console.WriteLine($"(prompt cache created: {result.Usage.CacheCreationInputTokens} tokens cached)");
                }
            }

            var toolUse = result?.Content?.FirstOrDefault(c => c.Type == "tool_use");
            if (toolUse?.Input != null)
            {
                var inputJson = toolUse.Input.Value.GetRawText();
                var toolInput = JsonSerializer.Deserialize(inputJson, ClaudeJsonContext.Default.GitToolInput);

                if (toolInput?.Commands != null)
                {
                    return toolInput.Commands.Select(c => c.ToGeneratedCommand()).ToList();
                }
            }

            var textContent = result?.Content?.FirstOrDefault(c => c.Type == "text")?.Text;
            if (!string.IsNullOrEmpty(textContent))
            {
                Console.Error.WriteLine("Warning: Model returned text instead of tool call");
                return [];
            }

            return [];
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException("Claude API request timed out after 60 seconds.");
        }
    }
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
    public object? InputSchema { get; set; }
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
