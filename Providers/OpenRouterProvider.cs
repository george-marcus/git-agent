using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using GitAgent.Configuration;
using GitAgent.Models;
using GitAgent.Services;

namespace GitAgent.Providers;

public class OpenRouterProvider : IModelProvider
{
    private readonly OpenRouterConfig _config;
    private readonly IPromptBuilder _promptBuilder;
    private readonly HttpClient _httpClient;

    public OpenRouterProvider(OpenRouterConfig config, IPromptBuilder promptBuilder, CachingHttpHandler cachingHandler)
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
        LogUsage(result);

        var toolInput = ExtractToolInput(result, OpenRouterJsonContext.Default.GitToolInput);
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

        var toolInput = ExtractToolInput(result, OpenRouterJsonContext.Default.ConflictToolInput);
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
            throw new InvalidOperationException("OpenRouter API key not configured. Run: git-agent config set openrouter.apiKey <your-key>");
        }
    }

    private OpenRouterRequest BuildRequest(string systemPrompt, string userPrompt, string toolName, string toolDescription, JsonSchema parameters, int maxTokens) => new()
    {
        Model = _config.Model,
        MaxTokens = maxTokens,
        Messages =
        [
            new OpenRouterMessage { Role = "system", Content = systemPrompt },
            new OpenRouterMessage { Role = "user", Content = userPrompt }
        ],
        Tools =
        [
            new OpenRouterTool
            {
                Type = "function",
                Function = new OpenRouterFunction { Name = toolName, Description = toolDescription, Parameters = parameters }
            }
        ],
        ToolChoice = new OpenRouterToolChoice { Type = "function", Function = new OpenRouterFunctionName { Name = toolName } }
    };

    private async Task<OpenRouterResponse?> SendRequestAsync(OpenRouterRequest request)
    {
        var json = JsonSerializer.Serialize(request, OpenRouterJsonContext.Default.OpenRouterRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);

        if (!string.IsNullOrWhiteSpace(_config.SiteUrl))
        {
            _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", _config.SiteUrl);
        }

        if (!string.IsNullOrWhiteSpace(_config.SiteName))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Title", _config.SiteName);
        }

        try
        {
            var response = await _httpClient.PostAsync("/api/v1/chat/completions", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"OpenRouter API error ({response.StatusCode}): {responseJson}");
            }

            return JsonSerializer.Deserialize(responseJson, OpenRouterJsonContext.Default.OpenRouterResponse);
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException("OpenRouter API request timed out after 60 seconds.");
        }
    }

    private static void LogUsage(OpenRouterResponse? result)
    {
        if (result?.Usage == null) return;

        var totalTokens = result.Usage.TotalTokens;
        if (totalTokens > 0)
        {
            Console.WriteLine($"(tokens used: {result.Usage.PromptTokens} prompt + {result.Usage.CompletionTokens} completion = {totalTokens} total)");
        }
    }

    private static T? ExtractToolInput<T>(OpenRouterResponse? result, JsonTypeInfo<T> typeInfo)
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

#region OpenRouter API Models

internal class OpenRouterRequest
{
    public string Model { get; set; } = "";
    public int MaxTokens { get; set; }
    public List<OpenRouterMessage> Messages { get; set; } = [];
    public List<OpenRouterTool>? Tools { get; set; }
    public OpenRouterToolChoice? ToolChoice { get; set; }
}

internal class OpenRouterMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
}

internal class OpenRouterTool
{
    public string Type { get; set; } = "function";
    public OpenRouterFunction? Function { get; set; }
}

internal class OpenRouterFunction
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public JsonSchema? Parameters { get; set; }
}

internal class OpenRouterToolChoice
{
    public string Type { get; set; } = "function";
    public OpenRouterFunctionName? Function { get; set; }
}

internal class OpenRouterFunctionName
{
    public string Name { get; set; } = "";
}

internal class OpenRouterResponse
{
    public List<OpenRouterChoice>? Choices { get; set; }
    public OpenRouterUsage? Usage { get; set; }
}

internal class OpenRouterChoice
{
    public OpenRouterResponseMessage? Message { get; set; }
}

internal class OpenRouterResponseMessage
{
    public string? Content { get; set; }
    public List<OpenRouterToolCall>? ToolCalls { get; set; }
}

internal class OpenRouterToolCall
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public OpenRouterFunctionCall? Function { get; set; }
}

internal class OpenRouterFunctionCall
{
    public string? Name { get; set; }
    public string? Arguments { get; set; }
}

internal class OpenRouterUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

#endregion
