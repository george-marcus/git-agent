using GitAgent.Configuration.ProviderConfigsModels;
using System.Text.Json.Serialization;

namespace GitAgent.Configuration;

public class GitAgentConfig
{
    [JsonPropertyName("configVersion")]
    public int ConfigVersion { get; set; } = 1;

    [JsonPropertyName("activeProvider")]
    public string ActiveProvider { get; set; } = "stub";

    [JsonPropertyName("providers")]
    public ProviderConfigs Providers { get; set; } = new();
}
