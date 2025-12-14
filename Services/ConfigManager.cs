using GitAgent.Configuration;
using System.Text.Json;

namespace GitAgent.Services;

public interface IConfigManager
{
    Task<GitAgentConfig> LoadAsync();
    Task SaveAsync(GitAgentConfig config);
    string ConfigPath { get; }
}

public class ConfigManager : IConfigManager
{
    private const int CurrentConfigVersion = 2;

    public string ConfigPath { get; }

    public ConfigManager()
    {
        var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".git-agent");

        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }

        ConfigPath = Path.Combine(configDir, "config.json");
    }

    public async Task<GitAgentConfig> LoadAsync()
    {
        if (!File.Exists(ConfigPath))
        {
            var defaultConfig = new GitAgentConfig { ConfigVersion = CurrentConfigVersion };
            await SaveAsync(defaultConfig);
            return defaultConfig;
        }

        try
        {
            var json = await File.ReadAllTextAsync(ConfigPath);
            var config = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.GitAgentConfig) ?? new GitAgentConfig();

            if (config.ConfigVersion < CurrentConfigVersion)
            {
                config.ConfigVersion = CurrentConfigVersion;
                await SaveAsync(config);
            }

            return config;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Warning: Failed to load config from {ConfigPath}: {ex.Message}");
            await Console.Error.WriteLineAsync("Using default configuration.");
            return new GitAgentConfig {ConfigVersion = CurrentConfigVersion };
        }
    }

    public async Task SaveAsync(GitAgentConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, ConfigJsonContext.Default.GitAgentConfig);
            await File.WriteAllTextAsync(ConfigPath, json);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: Failed to save config to {ConfigPath}: {ex.Message}");
            throw;
        }
    }
}
