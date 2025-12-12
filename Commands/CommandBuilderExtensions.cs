using System.CommandLine;
using System.CommandLine.Hosting;
using GitAgent.Configuration;
using GitAgent.Providers;
using GitAgent.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GitAgent.Commands;

public static class CommandBuilderExtensions
{
    public static RootCommand BuildRootCommand()
    {
        var root = new RootCommand("git-agent: Translate natural language to git commands using AI")
        {
            Name = "git-agent"
        };

        root.AddCommand(BuildRunCommand());
        root.AddCommand(BuildConfigCommand());
        root.AddCommand(BuildProvidersCommand());
        root.AddCommand(BuildCacheCommand());
        root.AddCommand(BuildHelpCommand());

        return root;
    }

    private static Command BuildRunCommand()
    {
        var runCmd = new Command("run", "Translate and optionally execute a plain English instruction");

        var instructionArg = new Argument<string>("instruction", "Natural language instruction to translate to git commands");
        var execOption = new Option<bool>(["--exec", "-x"], () => false, "Execute the resulting commands");
        var interactiveOption = new Option<bool>(["--interactive", "-i"], () => false, "Confirm each step interactively");
        var providerOption = new Option<string?>(["--provider", "-p"], "Override the active provider for this run");
        var noCacheOption = new Option<bool>(["--no-cache"], () => false, "Skip cache and force a fresh API call");

        runCmd.AddArgument(instructionArg);
        runCmd.AddOption(execOption);
        runCmd.AddOption(interactiveOption);
        runCmd.AddOption(providerOption);
        runCmd.AddOption(noCacheOption);

        runCmd.SetHandler(async context =>
        {
            var instruction = context.ParseResult.GetValueForArgument(instructionArg);
            var exec = context.ParseResult.GetValueForOption(execOption);
            var interactive = context.ParseResult.GetValueForOption(interactiveOption);
            var providerOverride = context.ParseResult.GetValueForOption(providerOption);
            var noCache = context.ParseResult.GetValueForOption(noCacheOption);

            var host = context.GetHost();
            var configManager = host.Services.GetRequiredService<IConfigManager>();
            var providerFactory = host.Services.GetRequiredService<IProviderFactory>();
            var gitInspector = host.Services.GetRequiredService<IGitInspector>();
            var safetyValidator = host.Services.GetRequiredService<ISafetyValidator>();
            var commandExecutor = host.Services.GetRequiredService<ICommandExecutor>();
            var cachingHandler = host.Services.GetRequiredService<CachingHttpHandler>();

            try
            {
                if (noCache)
                {
                    cachingHandler.ClearCache();
                }

                var provider = string.IsNullOrWhiteSpace(providerOverride)
                    ? await providerFactory.CreateProviderAsync()
                    : await providerFactory.CreateProviderAsync(providerOverride);

                var config = await configManager.LoadAsync();
                var activeProvider = providerOverride ?? config.ActiveProvider;
                await Console.Out.WriteLineAsync($"Using provider: {activeProvider}");
                await Console.Out.WriteLineAsync();

                var ctx = await gitInspector.BuildRepoContextAsync();

                if (string.IsNullOrWhiteSpace(ctx.CurrentBranch))
                {
                    await Console.Error.WriteLineAsync("Warning: Not in a git repository or git is not available.");
                    await Console.Error.WriteLineAsync("Commands will be generated but may not be accurate without repo context.");
                    await Console.Out.WriteLineAsync();
                }

                await Console.Out.WriteLineAsync("Generating commands...");
                var commands = await provider.GenerateGitCommands(instruction, ctx);

                if (commands.Count == 0)
                {
                    await Console.Out.WriteLineAsync("No git commands were generated for this instruction.");
                    return;
                }

                var validated = safetyValidator.FilterAndAnnotate(commands);

                await Console.Out.WriteLineAsync();
                await Console.Out.WriteLineAsync("Generated commands:");
                await Console.Out.WriteLineAsync(new string('-', 40));

                foreach (var cmd in validated)
                {
                    var riskColor = cmd.Risk switch
                    {
                        "safe" => "\u001b[32m",
                        "destructive" => "\u001b[31m",
                        _ => "\u001b[33m"
                    };
                    await Console.Out.WriteLineAsync($"  {riskColor}{cmd.CommandText}\u001b[0m");
                    if (!string.IsNullOrWhiteSpace(cmd.Reason))
                    {
                        await Console.Out.WriteLineAsync($"    ({cmd.Reason})");
                    }
                }

                await Console.Out.WriteLineAsync(new string('-', 40));

                if (commands.Count != validated.Count)
                {
                    var filtered = commands.Count - validated.Count;
                    await Console.Out.WriteLineAsync($"Note: {filtered} command(s) filtered out (not in allowlist).");
                }

                if (exec && validated.Count > 0)
                {
                    await Console.Out.WriteLineAsync();
                    await Console.Out.WriteLineAsync("Executing commands...");
                    await Console.Out.WriteLineAsync();
                    await commandExecutor.ExecuteAsync(validated, interactive);
                }
                else if (!exec && validated.Count > 0)
                {
                    await Console.Out.WriteLineAsync();
                    await Console.Out.WriteLineAsync("Add --exec (-x) to run these commands, or --exec --interactive (-xi) to confirm each.");
                }
            }
            catch (InvalidOperationException ex)
            {
                await Console.Error.WriteLineAsync($"Configuration error: {ex.Message}");
                context.ExitCode = 1;
            }
            catch (HttpRequestException ex)
            {
                await Console.Error.WriteLineAsync($"API error: {ex.Message}");
                context.ExitCode = 1;
            }
            catch (TimeoutException ex)
            {
                await Console.Error.WriteLineAsync($"Timeout: {ex.Message}");
                context.ExitCode = 1;
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Error: {ex.Message}");
                context.ExitCode = 1;
            }
        });

        return runCmd;
    }

    private static Command BuildConfigCommand()
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
        var providerArg = new Argument<string>("provider", "Provider to use (claude, openai, ollama, stub)");
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

    private static Command BuildProvidersCommand()
    {
        var providersCmd = new Command("providers", "List available AI providers");
        providersCmd.SetHandler(async context =>
        {
            var host = context.GetHost();
            var configManager = host.Services.GetRequiredService<IConfigManager>();
            var providerFactory = host.Services.GetRequiredService<IProviderFactory>();

            var config = await configManager.LoadAsync();
            await Console.Out.WriteLineAsync("Available providers:");
            foreach (var p in providerFactory.AvailableProviders)
            {
                var marker = p == config.ActiveProvider ? " (active)" : "";
                await Console.Out.WriteLineAsync($"  - {p}{marker}");
            }
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("Use 'git-agent config use <provider>' to switch providers.");
        });

        return providersCmd;
    }

    private static Command BuildCacheCommand()
    {
        var cacheCmd = new Command("cache", "Manage HTTP response cache");

        var cacheClearCmd = new Command("clear", "Clear all cached HTTP responses");
        cacheClearCmd.SetHandler(async context =>
        {
            var host = context.GetHost();
            var cachingHandler = host.Services.GetRequiredService<CachingHttpHandler>();
            cachingHandler.ClearCache();
            await Console.Out.WriteLineAsync("HTTP cache cleared.");
        });
        cacheCmd.AddCommand(cacheClearCmd);

        var cachePathCmd = new Command("path", "Show cache directory path");
        cachePathCmd.SetHandler(async context =>
        {
            var host = context.GetHost();
            var cachingHandler = host.Services.GetRequiredService<CachingHttpHandler>();
            await Console.Out.WriteLineAsync(cachingHandler.CacheDirectory);
        });
        cacheCmd.AddCommand(cachePathCmd);

        return cacheCmd;
    }

    private static Command BuildHelpCommand()
    {
        var helpCmd = new Command("help", "Show help and list all available commands");
        helpCmd.SetHandler(async () =>
        {
            await Console.Out.WriteLineAsync("git-agent: Translate natural language to git commands using AI");
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("Usage: git-agent [command] [options]");
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("Commands:");
            await Console.Out.WriteLineAsync("  run <instruction>   Translate and optionally execute a plain English instruction");
            await Console.Out.WriteLineAsync("  config              Manage git-agent configuration");
            await Console.Out.WriteLineAsync("    config show       Display current configuration");
            await Console.Out.WriteLineAsync("    config set        Set a configuration value");
            await Console.Out.WriteLineAsync("    config get        Get a configuration value");
            await Console.Out.WriteLineAsync("    config use        Set the active provider");
            await Console.Out.WriteLineAsync("    config path       Show the configuration file path");
            await Console.Out.WriteLineAsync("    config reset      Reset configuration to defaults");
            await Console.Out.WriteLineAsync("  providers           List available AI providers");
            await Console.Out.WriteLineAsync("  cache               Manage HTTP response cache");
            await Console.Out.WriteLineAsync("    cache clear       Clear all cached HTTP responses");
            await Console.Out.WriteLineAsync("    cache path        Show cache directory path");
            await Console.Out.WriteLineAsync("  help [command]      Show help for a command");
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("Options:");
            await Console.Out.WriteLineAsync("  --version           Show version information");
            await Console.Out.WriteLineAsync("  -?, -h, --help      Show help and usage information");
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("Examples:");
            await Console.Out.WriteLineAsync("  git-agent run \"commit all changes\"");
            await Console.Out.WriteLineAsync("  git-agent run \"push to origin\" --exec");
            await Console.Out.WriteLineAsync("  git-agent run \"merge feature into main\" -xi");
            await Console.Out.WriteLineAsync("  git-agent config set claude.apiKey sk-ant-xxx");
            await Console.Out.WriteLineAsync("  git-agent config use openai");
        });

        return helpCmd;
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
            "ollama.model" => config.Providers.Ollama.Model,
            "ollama.baseurl" => config.Providers.Ollama.BaseUrl,
            _ => null
        };
    }

    public static async Task PrintAvailableKeysAsync()
    {
        await Console.Error.WriteLineAsync("Available keys:");
        await Console.Error.WriteLineAsync("  activeProvider      - Active provider (claude, openai, ollama, stub)");
        await Console.Error.WriteLineAsync("  claude.apiKey       - Claude API key");
        await Console.Error.WriteLineAsync("  claude.model        - Claude model name");
        await Console.Error.WriteLineAsync("  claude.baseUrl      - Claude API base URL");
        await Console.Error.WriteLineAsync("  openai.apiKey       - OpenAI API key");
        await Console.Error.WriteLineAsync("  openai.model        - OpenAI model name");
        await Console.Error.WriteLineAsync("  openai.baseUrl      - OpenAI API base URL");
        await Console.Error.WriteLineAsync("  ollama.model        - Ollama model name");
        await Console.Error.WriteLineAsync("  ollama.baseUrl      - Ollama API base URL");
    }
}
