using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using GitAgent.Commands;
using GitAgent.Providers;
using GitAgent.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = new CommandLineBuilder(CommandBuilderExtensions.BuildRootCommand())
    .UseHost(_ => Host.CreateDefaultBuilder(args), hostBuilder =>
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
            services.AddSingleton<IJsonRpcServer, JsonRpcServer>();
        });
    })
    .UseDefaults();

return await builder.Build().InvokeAsync(args);
