using System.Text.Json;
using System.Text.Json.Serialization;
using GitAgent.Configuration;
using GitAgent.Models;
using GitAgent.Providers;
using GitAgent.Services;

namespace GitAgent;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(GitAgentConfig))]
[JsonSerializable(typeof(ProviderConfigs))]
[JsonSerializable(typeof(ClaudeConfig))]
[JsonSerializable(typeof(OpenAIConfig))]
[JsonSerializable(typeof(OllamaConfig))]
internal partial class ConfigJsonContext : JsonSerializerContext;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(CachedHttpResponse))]
internal partial class CacheJsonContext : JsonSerializerContext;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ClaudeRequest))]
[JsonSerializable(typeof(ClaudeResponse))]
[JsonSerializable(typeof(GitToolInput))]
internal partial class ClaudeJsonContext : JsonSerializerContext;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OpenAIRequest))]
[JsonSerializable(typeof(OpenAIResponse))]
[JsonSerializable(typeof(GitToolInput))]
internal partial class OpenAIJsonContext : JsonSerializerContext;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(OllamaRequest))]
[JsonSerializable(typeof(OllamaResponse))]
internal partial class OllamaJsonContext : JsonSerializerContext;
