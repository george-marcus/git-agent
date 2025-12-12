using System.Text.Json.Serialization;

namespace GitAgent.Configuration;

public class GitAgentConfig
{
    [JsonPropertyName("activeProvider")]
    public string ActiveProvider { get; set; } = "stub";

    [JsonPropertyName("providers")]
    public ProviderConfigs Providers { get; set; } = new();
}
