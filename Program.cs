using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using GitAgent.Commands;
using GitAgent.Configuration;
using GitAgent.Providers;
using GitAgent.Services.AI;
using GitAgent.Services.Execution;
using GitAgent.Services.Git;
using GitAgent.Services.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = new CommandLineBuilder(CommandsBuilder.BuildRootCommand())
    .UseHost(
        _ => Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
            }),
        hostBuilder =>
        {
            hostBuilder.ConfigureServices((_, services) =>
            {
                services.AddSingleton<IConfigManager, ConfigManager>();
                services.AddSingleton<IPromptBuilder, PromptBuilder>();
                services.AddSingleton<IResponseParser, ResponseParser>();
                services.AddSingleton<CachingHttpHandler>();
                services.AddSingleton<IProviderFactory, ProviderFactory>();
                services.AddSingleton<IGitInspector, GitInspector>();
                services.AddSingleton<ISafetyValidator, SafetyValidator>();
                services.AddSingleton<ICommandExecutor, CommandExecutor>();
                services.AddSingleton<IConflictResolver, ConflictResolver>();
                services.AddSingleton<ICompletionGenerator, CompletionGenerator>();
            });
        })
    .UseDefaults();

return await builder.Build().InvokeAsync(args);
