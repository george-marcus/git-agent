using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using GitAgent.Configuration;
using GitAgent.Models;
using GitAgent.Services;

namespace GitAgent.Providers;

public class OpenAIProvider : IModelProvider
{
    private readonly OpenAIConfig _config;
    private readonly IPromptBuilder _promptBuilder;
    private readonly HttpClient _httpClient;

    public OpenAIProvider(OpenAIConfig config, IPromptBuilder promptBuilder, CachingHttpHandler cachingHandler)
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

        var usePrompt = _promptBuilder.BuildCommandUserPrompt(instruction, context);
        var request = BuildRequest(GitTools.GitCommandSystemPrompt, usePrompt, GitTools.ToolName, GitTools.ToolDescription, GitTools.GetInputSchema(), 1024);

        var result = await SendRequestAsync(request);
        LogCacheUsage(result);

        var toolInput = ExtractToolInput(result, OpenAIJsonContext.Default.GitToolInput);
        if (toolInput?.Commands != null)
        {
            return toolInput.Commands.Select(c => c.ToGeneratedCommand()).ToList();
        }

        if (!string.IsNullOrEmpty(result?.Choices?.FirstOrDefault()?.Message?.Content))
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

        var toolInput = ExtractToolInput(result, OpenAIJsonContext.Default.ConflictToolInput);
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
            throw new InvalidOperationException("OpenAI API key not configured. Run: git-agent config set openai.apiKey <your-key>");
        }
    }

    private OpenAIRequest BuildRequest(string systemPrompt, string userPrompt, string toolName, string toolDescription, object parameters, int maxTokens) => new()
    {
        Model = _config.Model,
        MaxCompletionTokens = maxTokens,
        Messages =
        [
            new OpenAIRequestMessage { Role = "system", Content = systemPrompt },
            new OpenAIRequestMessage { Role = "user", Content = userPrompt }
        ],
        Tools =
        [
            new OpenAITool
            {
                Type = "function",
                Function = new OpenAIFunction { Name = toolName, Description = toolDescription, Parameters = parameters }
            }
        ],
        ToolChoice = new OpenAIToolChoice { Type = "function", Function = new OpenAIFunctionName { Name = toolName } }
    };

    private async Task<OpenAIResponse?> SendRequestAsync(OpenAIRequest request)
    {
        var json = JsonSerializer.Serialize(request, OpenAIJsonContext.Default.OpenAIRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);

        try
        {
            var response = await _httpClient.PostAsync("/v1/chat/completions", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"OpenAI API error ({response.StatusCode}): {responseJson}");
            }

            return JsonSerializer.Deserialize(responseJson, OpenAIJsonContext.Default.OpenAIResponse);
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException("OpenAI API request timed out after 60 seconds.");
        }
    }

    private static void LogCacheUsage(OpenAIResponse? result)
    {
        var cachedTokens = result?.Usage?.PromptTokensDetails?.CachedTokens ?? 0;
        if (cachedTokens > 0)
        {
            Console.WriteLine($"(prompt cache hit: {cachedTokens} tokens from cache)");
        }
    }

    private static T? ExtractToolInput<T>(OpenAIResponse? result, JsonTypeInfo<T> typeInfo)
    {
        var arguments = result?.Choices?.FirstOrDefault()?.Message?.ToolCalls?.FirstOrDefault()?.Function?.Arguments;
        if (arguments == null) return default;

        return JsonSerializer.Deserialize(arguments, typeInfo);
    }

    private static ResolutionConfidence ParseConfidence(string confidence) => confidence.ToLowerInvariant() switch
    {
        "high" => ResolutionConfidence.High,
        "medium" => ResolutionConfidence.Medium,
        _ => ResolutionConfidence.Low
    };
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
