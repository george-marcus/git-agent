using GitAgent.Configuration;
using GitAgent.Providers;
using GitAgent.Services;
using System.CommandLine;
using System.CommandLine.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace GitAgent.Commands
{
    internal class ConfigCommand
    {
        public static Command BuildConfigCommand()
        {
            var configCmd = new Command("config", "Manage git-agent configuration");

            configCmd.AddCommand(BuildConfigShowCommand());
            configCmd.AddCommand(BuildConfigSetCommand());
            configCmd.AddCommand(BuildConfigGetCommand());
            configCmd.AddCommand(BuildConfigUseCommand());
            configCmd.AddCommand(BuildConfigPathCommand());
            configCmd.AddCommand(BuildConfigResetCommand());

            return configCmd;
        }

        private static Command BuildConfigShowCommand()
        {
            var showCmd = new Command("show", "Display current configuration");
            showCmd.SetHandler(async context =>
            {
                var host = context.GetHost();
                var configManager = host.Services.GetRequiredService<IConfigManager>();

                var config = await configManager.LoadAsync();
                await Console.Out.WriteLineAsync($"Configuration file: {configManager.ConfigPath}");
                await Console.Out.WriteLineAsync();
                await Console.Out.WriteLineAsync($"Active provider: {config.ActiveProvider}");
                await Console.Out.WriteLineAsync();
                await Console.Out.WriteLineAsync("Provider settings:");
                await Console.Out.WriteLineAsync($"  Claude:");
                await Console.Out.WriteLineAsync($"    API Key: {ConfigHelpers.MaskApiKey(config.Providers.Claude.ApiKey)}");
                await Console.Out.WriteLineAsync($"    Model:   {config.Providers.Claude.Model}");
                await Console.Out.WriteLineAsync($"    BaseUrl: {config.Providers.Claude.BaseUrl}");
                await Console.Out.WriteLineAsync();
                await Console.Out.WriteLineAsync($"  OpenAI:");
                await Console.Out.WriteLineAsync($"    API Key: {ConfigHelpers.MaskApiKey(config.Providers.OpenAI.ApiKey)}");
                await Console.Out.WriteLineAsync($"    Model:   {config.Providers.OpenAI.Model}");
                await Console.Out.WriteLineAsync($"    BaseUrl: {config.Providers.OpenAI.BaseUrl}");
                await Console.Out.WriteLineAsync();
                await Console.Out.WriteLineAsync($"  OpenRouter:");
                await Console.Out.WriteLineAsync($"    API Key: {ConfigHelpers.MaskApiKey(config.Providers.OpenRouter.ApiKey)}");
                await Console.Out.WriteLineAsync($"    Model:   {config.Providers.OpenRouter.Model}");
                await Console.Out.WriteLineAsync($"    BaseUrl: {config.Providers.OpenRouter.BaseUrl}");
                await Console.Out.WriteLineAsync($"    Site Name: {config.Providers.OpenRouter.SiteName}");
                await Console.Out.WriteLineAsync($"    Site Url: {config.Providers.OpenRouter.SiteUrl}");
                await Console.Out.WriteLineAsync();
                await Console.Out.WriteLineAsync($"  Ollama:");
                await Console.Out.WriteLineAsync($"    Model:   {config.Providers.Ollama.Model}");
                await Console.Out.WriteLineAsync($"    BaseUrl: {config.Providers.Ollama.BaseUrl}");
            });
            return showCmd;
        }

        private static Command BuildConfigSetCommand()
        {
            var keyArg = new Argument<string>("key", "Configuration key (e.g., activeProvider, claude.apiKey)");
            var valueArg = new Argument<string>("value", "Value to set");
            var setCmd = new Command("set", "Set a configuration value") { keyArg, valueArg };
            setCmd.SetHandler(async context =>
            {
                var key = context.ParseResult.GetValueForArgument(keyArg);
                var value = context.ParseResult.GetValueForArgument(valueArg);

                var host = context.GetHost();
                var configManager = host.Services.GetRequiredService<IConfigManager>();

                var config = await configManager.LoadAsync();
                var updated = ConfigHelpers.SetConfigValue(config, key, value);
                if (updated)
                {
                    await configManager.SaveAsync(config);
                    await Console.Out.WriteLineAsync($"Set {key} = {(key.Contains("apiKey", StringComparison.OrdinalIgnoreCase) ? ConfigHelpers.MaskApiKey(value) : value)}");
                }
                else
                {
                    await Console.Error.WriteLineAsync($"Unknown configuration key: {key}");
                    await Console.Error.WriteLineAsync();
                    await ConfigHelpers.PrintAvailableKeysAsync();
                }
            });
            return setCmd;
        }

        private static Command BuildConfigGetCommand()
        {
            var keyArg = new Argument<string>("key", "Configuration key to retrieve");
            var getCmd = new Command("get", "Get a configuration value") { keyArg };
            getCmd.SetHandler(async context =>
            {
                var key = context.ParseResult.GetValueForArgument(keyArg);

                var host = context.GetHost();
                var configManager = host.Services.GetRequiredService<IConfigManager>();

                var config = await configManager.LoadAsync();
                var value = ConfigHelpers.GetConfigValue(config, key);
                if (value != null)
                {
                    var displayValue = key.Contains("apiKey", StringComparison.OrdinalIgnoreCase)
                        ? ConfigHelpers.MaskApiKey(value)
                        : value;
                    await Console.Out.WriteLineAsync(displayValue);
                }
                else
                {
                    await Console.Error.WriteLineAsync($"Unknown configuration key: {key}");
                    await ConfigHelpers.PrintAvailableKeysAsync();
                }
            });
            return getCmd;
        }

        private static Command BuildConfigUseCommand()
        {
            var providerArg = new Argument<string>("provider", "Provider to use (claude, openai, openrouter ,ollama, stub)");
            var useCmd = new Command("use", "Set the active provider") { providerArg };
            useCmd.SetHandler(async context =>
            {
                var provider = context.ParseResult.GetValueForArgument(providerArg);

                var host = context.GetHost();
                var configManager = host.Services.GetRequiredService<IConfigManager>();
                var providerFactory = host.Services.GetRequiredService<IProviderFactory>();

                var providerLower = provider.ToLowerInvariant();
                if (!providerFactory.AvailableProviders.Contains(providerLower))
                {
                    await Console.Error.WriteLineAsync($"Unknown provider: {provider}");
                    await Console.Error.WriteLineAsync($"Available providers: {string.Join(", ", providerFactory.AvailableProviders)}");
                    return;
                }

                var config = await configManager.LoadAsync();
                config.ActiveProvider = providerLower;
                await configManager.SaveAsync(config);
                await Console.Out.WriteLineAsync($"Active provider set to: {providerLower}");
            });
            return useCmd;
        }
        
        private static Command BuildConfigPathCommand()
        {
            var pathCmd = new Command("path", "Show the configuration file path");
            pathCmd.SetHandler(async context =>
            {
                var host = context.GetHost();
                var configManager = host.Services.GetRequiredService<IConfigManager>();
                await Console.Out.WriteLineAsync(configManager.ConfigPath);
            });
            return pathCmd;
        }

        private static Command BuildConfigResetCommand()
        {
            var resetCmd = new Command("reset", "Reset configuration to defaults");
            resetCmd.SetHandler(async context =>
            {
                var host = context.GetHost();
                var configManager = host.Services.GetRequiredService<IConfigManager>();

                await Console.Out.WriteAsync("Reset configuration to defaults? (y/N): ");
                var input = Console.ReadLine();
                if (string.Equals(input, "y", StringComparison.OrdinalIgnoreCase))
                {
                    await configManager.SaveAsync(new GitAgentConfig());
                    await Console.Out.WriteLineAsync("Configuration reset to defaults.");
                }
                else
                {
                    await Console.Out.WriteLineAsync("Cancelled.");
                }
            });
            return resetCmd;
        }
    }
}

internal static class ConfigHelpers
{
    public static string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return "(not set)";

        if (apiKey.Length <= 8)
            return "****";

        return apiKey[..4] + "****" + apiKey[^4..];
    }

    public static bool SetConfigValue(GitAgentConfig config, string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "activeprovider":
            case "provider":
                config.ActiveProvider = value.ToLowerInvariant();
                return true;
            case "claude.apikey":
                config.Providers.Claude.ApiKey = value;
                return true;
            case "claude.model":
                config.Providers.Claude.Model = value;
                return true;
            case "claude.baseurl":
                config.Providers.Claude.BaseUrl = value;
                return true;
            case "openai.apikey":
                config.Providers.OpenAI.ApiKey = value;
                return true;
            case "openai.model":
                config.Providers.OpenAI.Model = value;
                return true;
            case "openai.baseurl":
                config.Providers.OpenAI.BaseUrl = value;
                return true;
            case "openrouter.apikey":
                config.Providers.OpenRouter.ApiKey = value;
                return true;
            case "openrouter.model":
                config.Providers.OpenRouter.Model = value;
                return true;
            case "openrouter.baseurl":
                config.Providers.OpenRouter.BaseUrl = value;
                return true;
            case "openrouter.sitename":
                config.Providers.OpenRouter.SiteName = value;
                return true;
            case "openrouter.siteurl":
                config.Providers.OpenRouter.SiteUrl = value;
                return true;
            case "ollama.model":
                config.Providers.Ollama.Model = value;
                return true;
            case "ollama.baseurl":
                config.Providers.Ollama.BaseUrl = value;
                return true;

            default:
                return false;
        }
    }

    public static string? GetConfigValue(GitAgentConfig config, string key)
    {
        return key.ToLowerInvariant() switch
        {
            "activeprovider" or "provider" => config.ActiveProvider,
            "claude.apikey" => config.Providers.Claude.ApiKey,
            "claude.model" => config.Providers.Claude.Model,
            "claude.baseurl" => config.Providers.Claude.BaseUrl,
            "openai.apikey" => config.Providers.OpenAI.ApiKey,
            "openai.model" => config.Providers.OpenAI.Model,
            "openai.baseurl" => config.Providers.OpenAI.BaseUrl,
            "openrouter.apikey" => config.Providers.OpenRouter.ApiKey,
            "openrouter.model" => config.Providers.OpenRouter.Model,
            "openrouter.baseurl" => config.Providers.OpenRouter.BaseUrl,
            "openrouter.sitename" => config.Providers.OpenRouter.SiteName,
            "openrouter.siteurl" => config.Providers.OpenRouter.SiteUrl,
            "ollama.model" => config.Providers.Ollama.Model,
            "ollama.baseurl" => config.Providers.Ollama.BaseUrl,
            _ => null
        };
    }

    public static async Task PrintAvailableKeysAsync()
    {
        await Console.Error.WriteLineAsync("Available keys:");
        await Console.Error.WriteLineAsync("  activeProvider      - Active provider (claude, openai, openrouter, ollama, stub)");
        await Console.Error.WriteLineAsync("  claude.apiKey       - Claude API key");
        await Console.Error.WriteLineAsync("  claude.model        - Claude model name");
        await Console.Error.WriteLineAsync("  claude.baseUrl      - Claude API base URL");
        await Console.Error.WriteLineAsync("  openai.apiKey       - OpenAI API key");
        await Console.Error.WriteLineAsync("  openai.model        - OpenAI model name");
        await Console.Error.WriteLineAsync("  openai.baseUrl      - OpenAI API base URL");
        await Console.Error.WriteLineAsync("  openrouter.apiKey   - OpenRouter API key");
        await Console.Error.WriteLineAsync("  openrouter.model    - OpenRouter model name");
        await Console.Error.WriteLineAsync("  openrouter.baseUrl  - OpenRouter API base URL");
        await Console.Error.WriteLineAsync("  openrouter.sitename - OpenRouter API Site Name");
        await Console.Error.WriteLineAsync("  openrouter.siteurl  - OpenRouter API Site Url");
        await Console.Error.WriteLineAsync("  ollama.model        - Ollama model name");
        await Console.Error.WriteLineAsync("  ollama.baseUrl      - Ollama API base URL");
    }
}

