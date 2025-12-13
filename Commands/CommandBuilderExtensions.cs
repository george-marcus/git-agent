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
        root.AddCommand(BuildConflictsCommand());
        root.AddCommand(BuildCompletionsCommand());
        root.AddCommand(BuildServeCommand());
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

    private static Command BuildConflictsCommand()
    {
        var conflictsCmd = new Command("conflicts", "Analyze and resolve merge conflicts with AI assistance");

        var suggestOption = new Option<bool>(["--suggest", "-s"], () => false, "Show AI-suggested resolutions");
        var resolveOption = new Option<bool>(["--resolve", "-r"], () => false, "Interactively resolve conflicts");
        var applyOption = new Option<bool>(["--apply", "-a"], () => false, "Auto-apply AI-suggested resolutions");
        var providerOption = new Option<string?>(["--provider", "-p"], "Override the active provider for AI suggestions");
        var fileArg = new Argument<string?>("file", () => null, "Specific file to analyze (optional)");

        conflictsCmd.AddOption(suggestOption);
        conflictsCmd.AddOption(resolveOption);
        conflictsCmd.AddOption(applyOption);
        conflictsCmd.AddOption(providerOption);
        conflictsCmd.AddArgument(fileArg);

        conflictsCmd.SetHandler(async context =>
        {
            var suggest = context.ParseResult.GetValueForOption(suggestOption);
            var resolve = context.ParseResult.GetValueForOption(resolveOption);
            var apply = context.ParseResult.GetValueForOption(applyOption);
            var providerOverride = context.ParseResult.GetValueForOption(providerOption);
            var file = context.ParseResult.GetValueForArgument(fileArg);

            var host = context.GetHost();
            var gitInspector = host.Services.GetRequiredService<IGitInspector>();
            var conflictResolver = host.Services.GetRequiredService<IConflictResolver>();
            var providerFactory = host.Services.GetRequiredService<IProviderFactory>();

            var ctx = await gitInspector.BuildRepoContextAsync();

            if (ctx.MergeState == Models.MergeState.None)
            {
                await Console.Out.WriteLineAsync("No merge in progress.");
                return;
            }

            if (ctx.ConflictedFiles.Count == 0)
            {
                await Console.Out.WriteLineAsync($"Merge state: {ctx.MergeState}");
                await Console.Out.WriteLineAsync("No conflicts detected. You can continue with:");
                await Console.Out.WriteLineAsync($"  git {GetContinueCommand(ctx.MergeState)}");
                return;
            }

            var filesToAnalyze = ctx.ConflictedFiles;
            if (!string.IsNullOrEmpty(file))
            {
                filesToAnalyze = ctx.ConflictedFiles.Where(f => f.FilePath.Contains(file)).ToList();
                if (filesToAnalyze.Count == 0)
                {
                    await Console.Error.WriteLineAsync($"No conflicts found in files matching: {file}");
                    return;
                }
            }

            var analysis = await conflictResolver.AnalyzeConflictsAsync(ctx);

            await Console.Out.WriteLineAsync($"\u001b[33mMerge State:\u001b[0m {ctx.MergeState}");
            await Console.Out.WriteLineAsync($"\u001b[33mTotal Conflicts:\u001b[0m {analysis.TotalConflicts} in {analysis.ConflictedFileCount} file(s)");
            await Console.Out.WriteLineAsync();

            foreach (var fileAnalysis in analysis.Files)
            {
                await Console.Out.WriteLineAsync($"\u001b[36m{fileAnalysis.FilePath}\u001b[0m ({fileAnalysis.ConflictCount} conflict(s))");

                foreach (var section in fileAnalysis.Sections)
                {
                    var typeColor = section.ConflictType switch
                    {
                        Services.ConflictType.SameChange => "\u001b[32m",
                        Services.ConflictType.AdjacentChanges => "\u001b[33m",
                        _ => "\u001b[31m"
                    };
                    await Console.Out.WriteLineAsync($"  Lines {section.StartLine}-{section.EndLine}: {typeColor}{section.ConflictType}\u001b[0m");
                    await Console.Out.WriteLineAsync($"    {section.Description}");
                    await Console.Out.WriteLineAsync($"    Ours: {section.OursLabel} | Theirs: {section.TheirsLabel}");
                }
                await Console.Out.WriteLineAsync();
            }

            if (suggest || apply)
            {
                // Get provider for AI suggestions
                Providers.IModelProvider? provider = null;
                try
                {
                    provider = string.IsNullOrWhiteSpace(providerOverride)
                        ? await providerFactory.CreateProviderAsync()
                        : await providerFactory.CreateProviderAsync(providerOverride);

                    if (suggest)
                    {
                        await Console.Out.WriteLineAsync($"Using provider: {providerOverride ?? "default"}");
                    }
                }
                catch (Exception ex)
                {
                    await Console.Error.WriteLineAsync($"Warning: Could not initialize AI provider: {ex.Message}");
                    await Console.Error.WriteLineAsync("Falling back to heuristic suggestions.");
                }

                await Console.Out.WriteLineAsync("\u001b[33mGenerating AI Resolutions...\u001b[0m");
                var resolutions = await conflictResolver.SuggestResolutionsAsync(ctx, provider);

                if (suggest)
                {
                    await Console.Out.WriteLineAsync(new string('-', 40));
                    var grouped = resolutions.GroupBy(r => r.FilePath);

                    foreach (var group in grouped)
                    {
                        await Console.Out.WriteLineAsync($"\u001b[36m{group.Key}\u001b[0m");
                        var sectionGroups = group.GroupBy(r => (r.Section.StartLine, r.Section.EndLine));

                        foreach (var sectionGroup in sectionGroups)
                        {
                            await Console.Out.WriteLineAsync($"  Lines {sectionGroup.Key.StartLine}-{sectionGroup.Key.EndLine}:");
                            int optionNum = 1;
                            foreach (var resolution in sectionGroup)
                            {
                                var strategyColor = resolution.Strategy switch
                                {
                                    Services.ResolutionStrategy.AiSuggested => "\u001b[35m",
                                    Services.ResolutionStrategy.AcceptOurs => "\u001b[32m",
                                    Services.ResolutionStrategy.AcceptTheirs => "\u001b[34m",
                                    _ => "\u001b[33m"
                                };
                                await Console.Out.WriteLineAsync($"    {optionNum}. {strategyColor}[{resolution.Strategy}]\u001b[0m {resolution.Description}");
                                optionNum++;
                            }
                        }
                        await Console.Out.WriteLineAsync();
                    }
                }

                if (apply)
                {
                    await Console.Out.WriteLineAsync();
                    await Console.Out.WriteLineAsync("\u001b[33mApplying AI Resolutions...\u001b[0m");
                    await Console.Out.WriteLineAsync(new string('-', 40));

                    // Get only AI-suggested resolutions
                    var aiResolutions = resolutions
                        .Where(r => r.Strategy == Services.ResolutionStrategy.AiSuggested)
                        .ToList();

                    if (aiResolutions.Count == 0)
                    {
                        await Console.Out.WriteLineAsync("No AI resolutions available to apply.");
                    }
                    else
                    {
                        var groupedByFile = aiResolutions.GroupBy(r => r.FilePath);
                        int appliedCount = 0;
                        int failedCount = 0;

                        foreach (var fileGroup in groupedByFile)
                        {
                            await Console.Out.WriteAsync($"  {fileGroup.Key}: ");
                            var fileResolutions = fileGroup.ToList();

                            var success = await conflictResolver.ApplyAllResolutionsAsync(fileResolutions, fileGroup.Key);
                            if (success)
                            {
                                await Console.Out.WriteLineAsync($"\u001b[32m{fileResolutions.Count} conflict(s) resolved\u001b[0m");
                                appliedCount += fileResolutions.Count;
                            }
                            else
                            {
                                await Console.Out.WriteLineAsync("\u001b[31mFailed to apply\u001b[0m");
                                failedCount += fileResolutions.Count;
                            }
                        }

                        await Console.Out.WriteLineAsync();
                        await Console.Out.WriteLineAsync($"Applied: {appliedCount}, Failed: {failedCount}");

                        if (appliedCount > 0)
                        {
                            await Console.Out.WriteLineAsync();
                            await Console.Out.WriteLineAsync("Next steps:");
                            await Console.Out.WriteLineAsync("  1. Review the resolved files");
                            await Console.Out.WriteLineAsync("  2. git add <resolved-files>");
                            await Console.Out.WriteLineAsync($"  3. git {GetContinueCommand(ctx.MergeState)}");
                        }
                    }
                }
            }

            if (resolve)
            {
                await Console.Out.WriteLineAsync("\u001b[33mInteractive Resolution Mode\u001b[0m");
                await Console.Out.WriteLineAsync("For each conflict, choose a resolution strategy:");
                await Console.Out.WriteLineAsync();

                var resolvedFiles = new List<string>();

                foreach (var conflictFile in filesToAnalyze)
                {
                    await Console.Out.WriteLineAsync($"\u001b[36m{conflictFile.FilePath}\u001b[0m");
                    var fileResolved = false;

                    foreach (var section in conflictFile.Sections)
                    {
                        await Console.Out.WriteLineAsync($"\n  Conflict at lines {section.StartLine}-{section.EndLine}:");
                        await Console.Out.WriteLineAsync($"  \u001b[32mOurs ({section.OursLabel}):\u001b[0m");
                        PrintIndented(section.OursContent, "    ");
                        await Console.Out.WriteLineAsync($"  \u001b[34mTheirs ({section.TheirsLabel}):\u001b[0m");
                        PrintIndented(section.TheirsContent, "    ");

                        await Console.Out.WriteLineAsync();
                        await Console.Out.WriteLineAsync("  Options:");
                        await Console.Out.WriteLineAsync("    1. Accept ours");
                        await Console.Out.WriteLineAsync("    2. Accept theirs");
                        await Console.Out.WriteLineAsync("    3. Combine (ours + theirs)");
                        await Console.Out.WriteLineAsync("    4. Skip this conflict");

                        await Console.Out.WriteAsync("  Choose [1-4]: ");
                        var input = Console.ReadLine()?.Trim();

                        string? resolvedContent = input switch
                        {
                            "1" => section.OursContent,
                            "2" => section.TheirsContent,
                            "3" => section.OursContent + "\n" + section.TheirsContent,
                            _ => null
                        };

                        if (resolvedContent != null)
                        {
                            var resolution = new Services.ConflictResolution
                            {
                                FilePath = conflictFile.FilePath,
                                Section = section,
                                Strategy = Services.ResolutionStrategy.Manual,
                                Description = "Manual resolution",
                                ResolvedContent = resolvedContent
                            };

                            var success = await conflictResolver.ApplyResolutionAsync(resolution);
                            if (success)
                            {
                                await Console.Out.WriteLineAsync($"  \u001b[32mResolution applied.\u001b[0m");
                                fileResolved = true;
                            }
                            else
                            {
                                await Console.Out.WriteLineAsync($"  \u001b[31mFailed to apply resolution.\u001b[0m");
                            }
                        }
                        else
                        {
                            await Console.Out.WriteLineAsync($"  \u001b[33mSkipped.\u001b[0m");
                        }
                    }

                    if (fileResolved)
                    {
                        resolvedFiles.Add(conflictFile.FilePath);
                    }
                }

                await Console.Out.WriteLineAsync();
                if (resolvedFiles.Count > 0)
                {
                    await Console.Out.WriteLineAsync("Resolved files:");
                    foreach (var resolvedFile in resolvedFiles)
                    {
                        await Console.Out.WriteLineAsync($"  {resolvedFile}");
                    }
                    await Console.Out.WriteLineAsync();
                    await Console.Out.WriteLineAsync("Next steps:");
                    await Console.Out.WriteLineAsync($"  git add {string.Join(" ", resolvedFiles.Select(f => $"\"{f}\""))}");
                    await Console.Out.WriteLineAsync($"  git {GetContinueCommand(ctx.MergeState)}");
                }
                else
                {
                    await Console.Out.WriteLineAsync("No conflicts were resolved.");
                }
            }
            else if (!suggest && !apply)
            {
                await Console.Out.WriteLineAsync("Use --suggest (-s) to see AI-suggested resolutions.");
                await Console.Out.WriteLineAsync("Use --apply (-a) to auto-apply AI resolutions.");
                await Console.Out.WriteLineAsync("Use --resolve (-r) to interactively resolve conflicts.");
            }
        });

        return conflictsCmd;
    }

    private static string GetContinueCommand(Models.MergeState state) => state switch
    {
        Models.MergeState.Merging => "merge --continue",
        Models.MergeState.Rebasing => "rebase --continue",
        Models.MergeState.CherryPicking => "cherry-pick --continue",
        Models.MergeState.Reverting => "revert --continue",
        _ => "commit"
    };

    private static void PrintIndented(string content, string indent)
    {
        if (string.IsNullOrEmpty(content))
        {
            Console.WriteLine($"{indent}(empty)");
            return;
        }
        foreach (var line in content.Split('\n').Take(5))
        {
            Console.WriteLine($"{indent}{line}");
        }
        var lineCount = content.Split('\n').Length;
        if (lineCount > 5)
        {
            Console.WriteLine($"{indent}... ({lineCount - 5} more lines)");
        }
    }

    private static Command BuildCompletionsCommand()
    {
        var completionsCmd = new Command("completions", "Generate shell completion scripts");

        var shellArg = new Argument<string>("shell", "Shell type (bash, zsh, powershell, fish)");
        completionsCmd.AddArgument(shellArg);

        completionsCmd.SetHandler(async context =>
        {
            var shell = context.ParseResult.GetValueForArgument(shellArg);
            var host = context.GetHost();
            var generator = host.Services.GetRequiredService<ICompletionGenerator>();

            var script = shell.ToLowerInvariant() switch
            {
                "bash" => generator.GenerateBashCompletion(),
                "zsh" => generator.GenerateZshCompletion(),
                "powershell" or "pwsh" => generator.GeneratePowerShellCompletion(),
                "fish" => generator.GenerateFishCompletion(),
                _ => null
            };

            if (script == null)
            {
                await Console.Error.WriteLineAsync($"Unknown shell: {shell}");
                await Console.Error.WriteLineAsync("Supported shells: bash, zsh, powershell, fish");
                context.ExitCode = 1;
                return;
            }

            await Console.Out.WriteLineAsync(script);
            await Console.Error.WriteLineAsync();
            await Console.Error.WriteLineAsync($"# To install, add the above to your shell configuration:");
            await Console.Error.WriteLineAsync(shell.ToLowerInvariant() switch
            {
                "bash" => "#   Add to ~/.bashrc or ~/.bash_completion",
                "zsh" => "#   Add to ~/.zshrc or save to a file in $fpath",
                "powershell" or "pwsh" => "#   Add to your PowerShell profile ($PROFILE)",
                "fish" => "#   Save to ~/.config/fish/completions/git-agent.fish",
                _ => ""
            });
        });

        return completionsCmd;
    }

    private static Command BuildServeCommand()
    {
        var serveCmd = new Command("serve", "Start JSON-RPC server for IDE integration");

        var portOption = new Option<int>(["--port", "-p"], () => 9123, "Port to listen on");
        serveCmd.AddOption(portOption);

        serveCmd.SetHandler(async context =>
        {
            var port = context.ParseResult.GetValueForOption(portOption);
            var host = context.GetHost();
            var server = host.Services.GetRequiredService<IJsonRpcServer>();

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                await server.StartAsync(port, cts.Token);
            }
            catch (OperationCanceledException)
            {
                await Console.Out.WriteLineAsync("\nServer stopped.");
            }
        });

        return serveCmd;
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
            await Console.Out.WriteLineAsync("  conflicts           Analyze and resolve merge conflicts");
            await Console.Out.WriteLineAsync("    conflicts -s      Show AI-suggested resolutions");
            await Console.Out.WriteLineAsync("    conflicts -a      Auto-apply AI-suggested resolutions");
            await Console.Out.WriteLineAsync("    conflicts -r      Interactively resolve conflicts");
            await Console.Out.WriteLineAsync("  completions <shell> Generate shell completion scripts (bash, zsh, powershell, fish)");
            await Console.Out.WriteLineAsync("  serve               Start JSON-RPC server for IDE integration");
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
