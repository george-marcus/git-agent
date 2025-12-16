using FluentAssertions;
using GitAgent.Configuration.ProviderConfigsModels;
using GitAgent.Models;
using GitAgent.Providers;
using GitAgent.Services.AI;
using GitAgent.Services.Infrastructure;
using NSubstitute;

namespace GitAgentCli.Tests.Providers;

public class OpenRouterProviderTests
{
    private readonly OpenRouterConfig _config;
    private readonly IPromptBuilder _promptBuilder;

    public OpenRouterProviderTests()
    {
        _config = new OpenRouterConfig
        {
            ApiKey = "test-api-key",
            Model = "openai/gpt-4o",
            BaseUrl = "https://openrouter.ai",
            SiteName = "TestApp",
            SiteUrl = "https://test.example.com"
        };

        _promptBuilder = Substitute.For<IPromptBuilder>();
        
        _promptBuilder.BuildCommandUserPrompt(Arg.Any<string>(), Arg.Any<RepoContext>()).Returns("Test prompt");
        _promptBuilder.BuildConflictUserPrompt(Arg.Any<ConflictSection>(), Arg.Any<string>(), Arg.Any<string>()).Returns("Test conflict prompt");
    }

    [Fact]
    public void Constructor_WithValidConfig_CreatesInstance()
    {
        var cachingHandler = new CachingHttpHandler();

        var provider = new OpenRouterProvider(_config, _promptBuilder, cachingHandler);

        provider.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateGitCommands_WithoutApiKey_ThrowsInvalidOperationException()
    {
        var configWithoutKey = new OpenRouterConfig { ApiKey = "" };
        var cachingHandler = new CachingHttpHandler();
        var provider = new OpenRouterProvider(configWithoutKey, _promptBuilder, cachingHandler);

        var act = async () => await provider.GenerateGitCommands("test instruction", new RepoContext());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*OpenRouter API key not configured*");
    }

    [Fact]
    public async Task GenerateGitCommands_WithWhitespaceApiKey_ThrowsInvalidOperationException()
    {
        var configWithWhitespace = new OpenRouterConfig { ApiKey = "   " };
        var cachingHandler = new CachingHttpHandler();
        var provider = new OpenRouterProvider(configWithWhitespace, _promptBuilder, cachingHandler);

        var act = async () => await provider.GenerateGitCommands("test instruction", new RepoContext());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*OpenRouter API key not configured*");
    }

    [Fact]
    public async Task GenerateConflictResolution_WithoutApiKey_ThrowsInvalidOperationException()
    {
        var configWithoutKey = new OpenRouterConfig { ApiKey = "" };
        var cachingHandler = new CachingHttpHandler();
        var provider = new OpenRouterProvider(configWithoutKey, _promptBuilder, cachingHandler);
        var conflict = new ConflictSection { OursContent = "ours", TheirsContent = "theirs" };

        var act = async () => await provider.GenerateConflictResolution(conflict, "test.cs", ".cs");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*OpenRouter API key not configured*");
    }

    [Fact]
    public async Task GenerateGitCommands_WithNullApiKey_ThrowsInvalidOperationException()
    {
        var configWithNull = new OpenRouterConfig { ApiKey = null! };
        var cachingHandler = new CachingHttpHandler();
        var provider = new OpenRouterProvider(configWithNull, _promptBuilder, cachingHandler);

        var act = async () => await provider.GenerateGitCommands("test instruction", new RepoContext());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*OpenRouter API key not configured*");
    }
}

public class OpenRouterConfigTests
{
    [Fact]
    public void DefaultModel_IsOpenAIGpt4o()
    {
        var config = new OpenRouterConfig();

        config.Model.Should().Be("openai/gpt-4o");
    }

    [Fact]
    public void DefaultBaseUrl_IsOpenRouterAi()
    {
        var config = new OpenRouterConfig();

        config.BaseUrl.Should().Be("https://openrouter.ai");
    }

    [Fact]
    public void DefaultSiteName_IsGitAgent()
    {
        var config = new OpenRouterConfig();

        config.SiteName.Should().Be("GitAgent");
    }

    [Fact]
    public void DefaultSiteUrl_IsEmpty()
    {
        var config = new OpenRouterConfig();

        config.SiteUrl.Should().BeEmpty();
    }

    [Fact]
    public void DefaultApiKey_IsEmpty()
    {
        var config = new OpenRouterConfig();

        config.ApiKey.Should().BeEmpty();
    }

    [Fact]
    public void CanSetCustomModel()
    {
        var config = new OpenRouterConfig { Model = "anthropic/claude-3-opus" };

        config.Model.Should().Be("anthropic/claude-3-opus");
    }

    [Fact]
    public void CanSetCustomBaseUrl()
    {
        var config = new OpenRouterConfig { BaseUrl = "https://custom.api.com" };

        config.BaseUrl.Should().Be("https://custom.api.com");
    }

    [Fact]
    public void CanSetSiteMetadata()
    {
        var config = new OpenRouterConfig
        {
            SiteName = "MyApp",
            SiteUrl = "https://myapp.example.com"
        };

        config.SiteName.Should().Be("MyApp");
        config.SiteUrl.Should().Be("https://myapp.example.com");
    }
}
