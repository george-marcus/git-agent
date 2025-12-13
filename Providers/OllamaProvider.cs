using System.Text;
using System.Text.Json;
using GitAgent.Configuration;
using GitAgent.Models;
using GitAgent.Services;

namespace GitAgent.Providers;

public class OllamaProvider : IModelProvider
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
        var prompt = _promptBuilder.BuildPrompt(instruction, context);

        var requestBody = new OllamaRequest
        {
            Model = _config.Model,
            Prompt = prompt,
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
