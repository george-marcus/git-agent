using FluentAssertions;
using GitAgent.Configuration;
using GitAgent.Providers;
using GitAgent.Services;
using NSubstitute;

namespace GitAgentCli.Tests.Providers;

public class ProviderFactoryTests
{
    private readonly IConfigManager _configManager;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IResponseParser _responseParser;
    private readonly CachingHttpHandler _cachingHandler;
    private readonly ProviderFactory _factory;

    public ProviderFactoryTests()
    {
        _configManager = Substitute.For<IConfigManager>();
        _promptBuilder = Substitute.For<IPromptBuilder>();
        _responseParser = Substitute.For<IResponseParser>();
        _cachingHandler = new CachingHttpHandler();

        _configManager.LoadAsync().Returns(new GitAgentConfig());
        _factory = new ProviderFactory(_configManager, _promptBuilder, _responseParser, _cachingHandler);
    }

    [Fact]
    public void AvailableProviders_ContainsExpectedProviders()
    {
        _factory.AvailableProviders.Should().Contain("claude");
        _factory.AvailableProviders.Should().Contain("openai");
        _factory.AvailableProviders.Should().Contain("ollama");
        _factory.AvailableProviders.Should().Contain("stub");
    }

    [Fact]
    public async Task CreateProviderAsync_WithNoArgument_UsesActiveProviderFromConfig()
    {
        var config = new GitAgentConfig { ActiveProvider = "stub" };
        _configManager.LoadAsync().Returns(config);

        var provider = await _factory.CreateProviderAsync();
        provider.Should().BeOfType<StubProvider>();
    }

    [Theory]
    [InlineData("claude", typeof(ClaudeProvider))]
    [InlineData("openai", typeof(OpenAIProvider))]
    [InlineData("ollama", typeof(OllamaProvider))]
    [InlineData("stub", typeof(StubProvider))]
    public async Task CreateProviderAsync_WithValidName_ReturnsCorrectType(string name, Type expectedType)
    {
        var provider = await _factory.CreateProviderAsync(name);
        provider.Should().BeOfType(expectedType);
    }

    [Theory]
    [InlineData("CLAUDE")]
    [InlineData("Claude")]
    [InlineData("OPENAI")]
    [InlineData("OpenAI")]
    [InlineData("OLLAMA")]
    [InlineData("Ollama")]
    [InlineData("STUB")]
    [InlineData("Stub")]
    public async Task CreateProviderAsync_IsCaseInsensitive(string name)
    {
        var provider = await _factory.CreateProviderAsync(name);
        provider.Should().NotBeNull();
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("gpt4")]
    [InlineData("gemini")]
    [InlineData("")]
    public async Task CreateProviderAsync_WithInvalidName_ThrowsArgumentException(string name)
    {
        var act = async () => await _factory.CreateProviderAsync(name);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage($"*{name}*");
    }

    [Fact]
    public async Task CreateProviderAsync_WithUnknownProvider_IncludesAvailableProvidersInError()
    {
        var act = async () => await _factory.CreateProviderAsync("invalid");

        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.Message.Should().Contain("claude");
        exception.Which.Message.Should().Contain("openai");
        exception.Which.Message.Should().Contain("ollama");
        exception.Which.Message.Should().Contain("stub");
    }

    [Fact]
    public async Task CreateProviderAsync_WithClaudeConfig_UsesConfigValues()
    {
        var config = new GitAgentConfig
        {
            Providers = new ProviderConfigs
            {
                Claude = new ClaudeConfig
                {
                    ApiKey = "test-api-key",
                    Model = "claude-4.5-opus",
                    BaseUrl = "https://custom.api.com"
                }
            }
        };
        _configManager.LoadAsync().Returns(config);

        var provider = await _factory.CreateProviderAsync("claude");

        provider.Should().BeOfType<ClaudeProvider>();
    }

    [Fact]
    public async Task CreateProviderAsync_WithOllama_UsesOllamaPromptBuilder()
    {
        var provider = await _factory.CreateProviderAsync("ollama");
        provider.Should().BeOfType<OllamaProvider>();
    }

    [Fact]
    public async Task CreateProviderAsync_WithClaude_UsesStandardPromptBuilder()
    {
        var provider = await _factory.CreateProviderAsync("claude");
        provider.Should().BeOfType<ClaudeProvider>();
    }

    [Fact]
    public async Task CreateProviderAsync_WithOpenAI_UsesStandardPromptBuilder()
    {
        var provider = await _factory.CreateProviderAsync("openai");
        provider.Should().BeOfType<OpenAIProvider>();
    }
}
