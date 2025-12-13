using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GitAgent.Configuration;
using GitAgent.Models;
using GitAgent.Services;

namespace GitAgent.Providers;

public partial class OllamaProvider : IModelProvider
{
    private readonly OllamaConfig _config;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IResponseParser _responseParser;
    private readonly HttpClient _httpClient;

    public OllamaProvider(OllamaConfig config, IPromptBuilder promptBuilder, IResponseParser responseParser)
    {
        _config = config;
        _promptBuilder = promptBuilder;
        _responseParser = responseParser;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_config.BaseUrl),
            Timeout = TimeSpan.FromSeconds(120)
        };
    }

    public async Task<IReadOnlyList<GeneratedCommand>> GenerateGitCommands(string instruction, RepoContext context)
    {
        var userPrompt = _promptBuilder.BuildCommandUserPrompt(instruction, context);

        var requestBody = new OllamaRequest
        {
            Model = _config.Model,
            Prompt = userPrompt,
            Stream = false
        };

        var json = JsonSerializer.Serialize(requestBody, OllamaJsonContext.Default.OllamaRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync("/api/generate", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    responseJson.Contains("connection refused", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Ollama not reachable at {_config.BaseUrl}. Make sure Ollama is running.");
                }

                throw new HttpRequestException($"Ollama API error ({response.StatusCode}): {responseJson}");
            }

            var result = JsonSerializer.Deserialize(responseJson, OllamaJsonContext.Default.OllamaResponse);
            var textContent = result?.Response ?? "";

            return _responseParser.ParseResponse(textContent);
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            throw new InvalidOperationException($"Cannot connect to Ollama at {_config.BaseUrl}. Make sure Ollama is running with: ollama serve");
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException("Ollama request timed out after 120 seconds.");
        }
    }

    public async Task<ConflictResolutionResult> GenerateConflictResolution(ConflictSection conflict, string filePath, string fileExtension)
    {
        var userPrompt = _promptBuilder.BuildConflictUserPrompt(conflict, filePath, fileExtension);

        var requestBody = new OllamaRequest
        {
            Model = _config.Model,
            Prompt = userPrompt,
            Stream = false
        };

        var json = JsonSerializer.Serialize(requestBody, OllamaJsonContext.Default.OllamaRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync("/api/generate", content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    responseJson.Contains("connection refused", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Ollama not reachable at {_config.BaseUrl}. Make sure Ollama is running.");
                }

                throw new HttpRequestException($"Ollama API error ({response.StatusCode}): {responseJson}");
            }

            var result = JsonSerializer.Deserialize(responseJson, OllamaJsonContext.Default.OllamaResponse);
            var textContent = result?.Response ?? "";

            return ParseConflictResponse(textContent, conflict);
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            throw new InvalidOperationException($"Cannot connect to Ollama at {_config.BaseUrl}. Make sure Ollama is running with: ollama serve");
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException("Ollama request timed out after 120 seconds.");
        }
    }

    private static ConflictResolutionResult ParseConflictResponse(string response, ConflictSection conflict)
    {
        var result = new ConflictResolutionResult
        {
            ResolvedContent = conflict.OursContent,
            Explanation = "Failed to parse AI response, defaulting to 'ours'",
            Confidence = ResolutionConfidence.Low
        };

        var codeMatch = ResolvedCodeRegex().Match(response);
        if (codeMatch.Success)
        {
            result.ResolvedContent = codeMatch.Groups[1].Value.Trim();
        }

        var explanationMatch = ExplanationRegex().Match(response);
        if (explanationMatch.Success)
        {
            result.Explanation = explanationMatch.Groups[1].Value.Trim();
        }

        var confidenceMatch = ConfidenceRegex().Match(response);
        if (confidenceMatch.Success)
        {
            result.Confidence = confidenceMatch.Groups[1].Value.ToLowerInvariant() switch
            {
                "high" => ResolutionConfidence.High,
                "medium" => ResolutionConfidence.Medium,
                _ => ResolutionConfidence.Low
            };
        }

        return result;
    }

    [GeneratedRegex(@"RESOLVED_CODE:\s*```[\w]*\n?(.*?)```", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ResolvedCodeRegex();

    [GeneratedRegex(@"EXPLANATION:\s*(.+?)(?=CONFIDENCE:|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ExplanationRegex();

    [GeneratedRegex(@"CONFIDENCE:\s*(high|medium|low)", RegexOptions.IgnoreCase)]
    private static partial Regex ConfidenceRegex();
}

internal class OllamaRequest
{
    public string Model { get; set; } = "";
    public string Prompt { get; set; } = "";
    public bool Stream { get; set; }
}

internal class OllamaResponse
{
    public string? Response { get; set; }
}
