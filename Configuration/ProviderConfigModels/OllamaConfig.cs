using System.Text.Json.Serialization;

namespace GitAgent.Configuration.ProviderConfigsModels;

public class OllamaConfig
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "llama3.2";

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = "http://localhost:11434";
}
