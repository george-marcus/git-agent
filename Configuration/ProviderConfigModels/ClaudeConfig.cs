using System.Text.Json.Serialization;

namespace GitAgent.Configuration.ProviderConfigsModels;

public class ClaudeConfig
{
    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = "";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "claude-sonnet-4-5";

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = "https://api.anthropic.com";
}
