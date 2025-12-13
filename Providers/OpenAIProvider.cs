using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GitAgent.Configuration;
using GitAgent.Models;
using GitAgent.Services;

namespace GitAgent.Providers;

public class OpenAIProvider : IModelProvider
{
    private readonly OpenAIConfig _config;
    private readonly IPromptBuilder _promptBuilder;
    private readonly HttpClient _httpClient;

    private const string SystemPrompt = """
        You are a git command generator. Your task is to translate natural language instructions into git commands.

        Rules:
        - Use the execute_git_commands function to return the commands
        - Prefer safe operations (status, add, commit, push, branch, checkout, fetch, pull)
        - Mark destructive commands (reset --hard, clean -fd, force push, branch -D) with risk 'destructive'
        - Mark commands that modify state (commit, push, merge) with risk 'moderate'
        - Mark read-only commands (status, log, diff, branch --list) with risk 'safe'
        - Return commands in the order they should be executed
        """;

    public OpenAIProvider(OpenAIConfig config, IPromptBuilder promptBuilder, IResponseParser responseParser, CachingHttpHandler cachingHandler)
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
            throw new InvalidOperationException("OpenAI API key not configured. Run: git-agent config set openai.apiKey <your-key>");
        }

        var prompt = _promptBuilder.BuildPrompt(instruction, context);

        var requestBody = new OpenAIRequest
        {
            Model = _config.Model,
            MaxCompletionTokens = 1024,
            Messages =
            [
                new OpenAIRequestMessage { Role = "system", Content = SystemPrompt },
                new OpenAIRequestMessage { Role = "user", Content = prompt }
            ],
            Tools =
            [
                new OpenAITool
                {
                    Type = "function",
                    Function = new OpenAIFunction
                    {
                        Name = GitTools.ToolName,
                        Description = GitTools.ToolDescription,
                        Parameters = GitTools.GetInputSchema()
                    }
                }
            ],
            ToolChoice = new OpenAIToolChoice
            {
                Type = "function",
                Function = new OpenAIFunctionName { Name = GitTools.ToolName }
            }
        };

        var json = JsonSerializer.Serialize(requestBody, OpenAIJsonContext.Default.OpenAIRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config.ApiKey);

        try
        {
            var response = await _httpClient.PostAsync("/v1/chat/completions", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"OpenAI API error ({response.StatusCode}): {responseJson}");
            }

            var result = JsonSerializer.Deserialize(responseJson, OpenAIJsonContext.Default.OpenAIResponse);

            if (result?.Usage != null)
            {
                var cachedTokens = result.Usage.PromptTokensDetails?.CachedTokens ?? 0;
                if (cachedTokens > 0)
                {
                    Console.WriteLine($"(prompt cache hit: {cachedTokens} tokens from cache)");
                }
            }

            var toolCall = result?.Choices?.FirstOrDefault()?.Message?.ToolCalls?.FirstOrDefault();
            if (toolCall?.Function?.Arguments != null)
            {
                var toolInput = JsonSerializer.Deserialize(toolCall.Function.Arguments, OpenAIJsonContext.Default.GitToolInput);

                if (toolInput?.Commands != null)
                {
                    return toolInput.Commands.Select(c => c.ToGeneratedCommand()).ToList();
                }
            }

            var textContent = result?.Choices?.FirstOrDefault()?.Message?.Content;
            if (!string.IsNullOrEmpty(textContent))
            {
                Console.Error.WriteLine("Warning: Model returned text instead of tool call");
                return [];
            }

            return [];
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException("OpenAI API request timed out after 60 seconds.");
        }
    }
}

#region OpenAI API Models

internal class OpenAIRequest
{
    public string Model { get; set; } = "";
    public int MaxCompletionTokens { get; set; }
    public List<OpenAIRequestMessage> Messages { get; set; } = [];
    public List<OpenAITool>? Tools { get; set; }
    public OpenAIToolChoice? ToolChoice { get; set; }
}

internal class OpenAIRequestMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

internal class OpenAITool
{
    public string Type { get; set; } = "function";
    public OpenAIFunction? Function { get; set; }
}

internal class OpenAIFunction
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public object? Parameters { get; set; }
}

internal class OpenAIToolChoice
{
    public string Type { get; set; } = "function";
    public OpenAIFunctionName? Function { get; set; }
}

internal class OpenAIFunctionName
{
    public string Name { get; set; } = "";
}

internal class OpenAIResponse
{
    public List<OpenAIChoice>? Choices { get; set; }
    public OpenAIUsage? Usage { get; set; }
}

internal class OpenAIChoice
{
    public OpenAIMessage? Message { get; set; }
}

internal class OpenAIMessage
{
    public string? Content { get; set; }
    public List<OpenAIToolCall>? ToolCalls { get; set; }
}

internal class OpenAIToolCall
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public OpenAIFunctionCall? Function { get; set; }
}

internal class OpenAIFunctionCall
{
    public string? Name { get; set; }
    public string? Arguments { get; set; }
}

internal class OpenAIUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public OpenAIPromptTokensDetails? PromptTokensDetails { get; set; }
}

internal class OpenAIPromptTokensDetails
{
    public int CachedTokens { get; set; }
}

#endregion
