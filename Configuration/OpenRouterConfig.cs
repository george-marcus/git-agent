using System.Text.Json.Serialization;

namespace GitAgent.Configuration;

public class OpenRouterConfig
{
    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = "";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "openai/gpt-4o";

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = "https://openrouter.ai";

    [JsonPropertyName("siteName")]
    public string SiteName { get; set; } = "GitAgent";

    [JsonPropertyName("siteUrl")]
    public string SiteUrl { get; set; } = "";
}
