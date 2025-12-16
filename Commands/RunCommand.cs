using System.CommandLine;
using System.CommandLine.Hosting;
using GitAgent.Configuration;
using GitAgent.Providers;
using GitAgent.Services.Execution;
using GitAgent.Services.Git;
using GitAgent.Services.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace GitAgent.Commands
{
    internal class RunCommand
    {
        public static Command BuildRunCommand()
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
    }
}
