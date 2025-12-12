using System.Text.Json.Serialization;

namespace GitAgent.Configuration;

public class OpenAIConfig
{
    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = "";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "gpt-4o";

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = "https://api.openai.com";
}
