using GitAgent.Configuration;
using GitAgent.Providers;
using System.CommandLine;
using System.CommandLine.Hosting;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using GitAgent.Configuration.ProviderConfigsModels;

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

                foreach (var providerProp in typeof(ProviderConfigs).GetProperties())
                {
                    var providerInstance = providerProp.GetValue(config.Providers);
                    if (providerInstance == null) continue;

                    await Console.Out.WriteLineAsync($"  {providerProp.Name}:");

                    foreach (var prop in providerInstance.GetType().GetProperties())
                    {
                        var value = prop.GetValue(providerInstance)?.ToString() ?? "";
                        var displayValue = prop.Name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase)
                            ? ConfigHelpers.MaskApiKey(value)
                            : value;
                        await Console.Out.WriteLineAsync($"    {prop.Name}: {displayValue}");
                    }
                    await Console.Out.WriteLineAsync();
                }
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
        var keyLower = key.ToLowerInvariant();

        if (keyLower is "activeprovider" or "provider")
        {
            config.ActiveProvider = value.ToLowerInvariant();
            return true;
        }

        var (targetProp, providerInstance) = GetTargetProperty(config, key);
        if (providerInstance == null || targetProp == null)
        {
            return false;
        }
        if (targetProp == null || !targetProp.CanWrite) return false;

        var convertedValue = Convert.ChangeType(value, targetProp.PropertyType);
        targetProp.SetValue(providerInstance, convertedValue);
        return true;
    }

    public static string? GetConfigValue(GitAgentConfig config, string key)
    {
        var keyLower = key.ToLowerInvariant();
        if (keyLower is "activeprovider" or "provider")
        {
            return config.ActiveProvider;
        }

        var (targetProp, providerInstance) = GetTargetProperty(config, key);
        if (providerInstance == null || targetProp == null)
        {
            return null;
        }
        return targetProp?.GetValue(providerInstance)?.ToString();
    }

    public static async Task PrintAvailableKeysAsync()
    {
        await Console.Error.WriteLineAsync("Available keys:");
        await Console.Error.WriteLineAsync("  activeProvider - Active provider (claude, openai, openrouter, ollama, stub)");
        await Console.Error.WriteLineAsync();

        foreach (var providerProp in typeof(ProviderConfigs).GetProperties())
        {
            var providerType = providerProp.PropertyType;
            var providerName = providerProp.Name.ToLowerInvariant();

            foreach (var prop in providerType.GetProperties())
            {
                var jsonAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
                var keyName = jsonAttr?.Name ?? prop.Name;
                var description = prop.Name;
                await Console.Out.WriteLineAsync($"  {providerName}.{keyName,-12} - {providerProp.Name} {description}");
            }
        }
    }

    private static (PropertyInfo? TargetProperty, object? ProviderInstance) GetTargetProperty(GitAgentConfig config, string key)
    {
        var parts = key.Split('.', 2);
        if (parts.Length != 2) return default;

        var providerName = parts[0];
        var propertyName = parts[1];

        var providerProp = typeof(ProviderConfigs).GetProperties().FirstOrDefault(p => p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));
        if (providerProp == null) return default;

        var providerInstance = providerProp.GetValue(config.Providers);
        if (providerInstance == null) return default;

        var targetProp = providerInstance.GetType().GetProperties().FirstOrDefault(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
        return (targetProp, providerInstance);
    }
}

