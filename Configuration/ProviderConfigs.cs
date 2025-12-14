using System.Text.Json.Serialization;

namespace GitAgent.Configuration;

public class ProviderConfigs
{
    [JsonPropertyName("claude")]
    public ClaudeConfig Claude { get; set; } = new();

    [JsonPropertyName("openai")]
    public OpenAIConfig OpenAI { get; set; } = new();

    [JsonPropertyName("ollama")]
    public OllamaConfig Ollama { get; set; } = new();

    [JsonPropertyName("openrouter")]
    public OpenRouterConfig OpenRouter { get; set; } = new();
}
