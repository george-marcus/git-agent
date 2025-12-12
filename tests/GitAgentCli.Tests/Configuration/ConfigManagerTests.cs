using System.Text.Json;
using FluentAssertions;
using GitAgent.Configuration;
using GitAgent.Services;

namespace GitAgentCli.Tests.Configuration;

public class ConfigManagerTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testConfigPath;

    public ConfigManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"git-agent-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        _testConfigPath = Path.Combine(_testDir, "config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    private TestableConfigManager CreateManager() => new(_testConfigPath);

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_CreatesDefaultConfig()
    {
        var manager = CreateManager();

        var config = await manager.LoadAsync();

        config.Should().NotBeNull();
        config.ActiveProvider.Should().Be("stub");
        File.Exists(_testConfigPath).Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_WhenFileExists_ReturnsExistingConfig()
    {
        var existingConfig = new GitAgentConfig { ActiveProvider = "claude" };
        await File.WriteAllTextAsync(_testConfigPath, JsonSerializer.Serialize(existingConfig, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
        var manager = CreateManager();

        var config = await manager.LoadAsync();

        config.ActiveProvider.Should().Be("claude");
    }

    [Fact]
    public async Task LoadAsync_WithInvalidJson_ReturnsDefaultConfig()
    {
        await File.WriteAllTextAsync(_testConfigPath, "not valid json {{{");
        var manager = CreateManager();

        var config = await manager.LoadAsync();

        config.Should().NotBeNull();
        config.ActiveProvider.Should().Be("stub");
    }

    [Fact]
    public async Task SaveAsync_CreatesValidJsonFile()
    {
        var manager = CreateManager();
        var config = new GitAgentConfig
        {
            ActiveProvider = "openai",
            Providers = new ProviderConfigs
            {
                OpenAI = new OpenAIConfig { ApiKey = "test-key" }
            }
        };

        await manager.SaveAsync(config);

        File.Exists(_testConfigPath).Should().BeTrue();
        var json = await File.ReadAllTextAsync(_testConfigPath);
        var loaded = JsonSerializer.Deserialize<GitAgentConfig>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        loaded!.ActiveProvider.Should().Be("openai");
        loaded.Providers.OpenAI.ApiKey.Should().Be("test-key");
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsCorrectly()
    {
        var manager = CreateManager();
        var original = new GitAgentConfig
        {
            ActiveProvider = "claude",
            Providers = new ProviderConfigs
            {
                Claude = new ClaudeConfig
                {
                    ApiKey = "sk-ant-test",
                    Model = "claude-3-opus",
                    BaseUrl = "https://custom.api.com"
                },
                OpenAI = new OpenAIConfig
                {
                    ApiKey = "sk-openai-test",
                    Model = "gpt-4-turbo"
                },
                Ollama = new OllamaConfig
                {
                    Model = "mistral",
                    BaseUrl = "http://localhost:11434"
                }
            }
        };

        await manager.SaveAsync(original);
        var loaded = await manager.LoadAsync();

        loaded.ActiveProvider.Should().Be(original.ActiveProvider);
        loaded.Providers.Claude.ApiKey.Should().Be(original.Providers.Claude.ApiKey);
        loaded.Providers.Claude.Model.Should().Be(original.Providers.Claude.Model);
        loaded.Providers.Claude.BaseUrl.Should().Be(original.Providers.Claude.BaseUrl);
        loaded.Providers.OpenAI.ApiKey.Should().Be(original.Providers.OpenAI.ApiKey);
        loaded.Providers.OpenAI.Model.Should().Be(original.Providers.OpenAI.Model);
        loaded.Providers.Ollama.Model.Should().Be(original.Providers.Ollama.Model);
        loaded.Providers.Ollama.BaseUrl.Should().Be(original.Providers.Ollama.BaseUrl);
    }

    [Fact]
    public void ConfigPath_ReturnsExpectedPath()
    {
        var manager = CreateManager();

        manager.ConfigPath.Should().Be(_testConfigPath);
    }

    [Fact]
    public async Task SaveAsync_CreatesIndentedJson()
    {
        var manager = CreateManager();
        var config = new GitAgentConfig { ActiveProvider = "claude" };

        await manager.SaveAsync(config);

        var json = await File.ReadAllTextAsync(_testConfigPath);
        json.Should().Contain("\n");
        json.Should().Contain("  ");
    }
}

internal class TestableConfigManager : IConfigManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string ConfigPath { get; }

    public TestableConfigManager(string configPath)
    {
        ConfigPath = configPath;
        var dir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public async Task<GitAgentConfig> LoadAsync()
    {
        if (!File.Exists(ConfigPath))
        {
            var defaultConfig = new GitAgentConfig();
            await SaveAsync(defaultConfig);
            return defaultConfig;
        }

        try
        {
            var json = await File.ReadAllTextAsync(ConfigPath);
            return JsonSerializer.Deserialize<GitAgentConfig>(json, JsonOptions) ?? new GitAgentConfig();
        }
        catch
        {
            return new GitAgentConfig();
        }
    }

    public async Task SaveAsync(GitAgentConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(ConfigPath, json);
    }
}
