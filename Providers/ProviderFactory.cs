using GitAgent.Services;

namespace GitAgent.Providers;

public interface IProviderFactory
{
    Task<IModelProvider> CreateProviderAsync();
    Task<IModelProvider> CreateProviderAsync(string providerName);
    IReadOnlyList<string> AvailableProviders { get; }
    CachingHttpHandler HttpCacheHandler { get; }
}

public class ProviderFactory : IProviderFactory
{
    private readonly IConfigManager _configManager;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IResponseParser _responseParser;

    public CachingHttpHandler HttpCacheHandler { get; }

    public IReadOnlyList<string> AvailableProviders { get; } =
    [
        "claude",
        "openai",
        "openrouter",
        "ollama",
        "stub"
    ];

    public ProviderFactory(IConfigManager configManager, IPromptBuilder promptBuilder, IResponseParser responseParser, CachingHttpHandler cachingHandler)
    {
        _configManager = configManager;
        _promptBuilder = promptBuilder;
        _responseParser = responseParser;
        HttpCacheHandler = cachingHandler;
    }

    public async Task<IModelProvider> CreateProviderAsync()
    {
        var config = await _configManager.LoadAsync();
        return await CreateProviderAsync(config.ActiveProvider);
    }

    public async Task<IModelProvider> CreateProviderAsync(string providerName)
    {
        var config = await _configManager.LoadAsync();

        return providerName.ToLowerInvariant() switch
        {
            "claude" => new ClaudeProvider(config.Providers.Claude, _promptBuilder, HttpCacheHandler),
            "openai" => new OpenAIProvider(config.Providers.OpenAI, _promptBuilder, HttpCacheHandler),
            "openrouter" => new OpenRouterProvider(config.Providers.OpenRouter, _promptBuilder, HttpCacheHandler),
            "ollama" => new OllamaProvider(config.Providers.Ollama, new OllamaPromptBuilder(), _responseParser),
            "stub" => new StubProvider(),
            _ => throw new ArgumentException($"Unknown provider: '{providerName}'. Available: {string.Join(", ", AvailableProviders)}")
        };
    }
}
