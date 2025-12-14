using GitAgent.Providers;
using GitAgent.Services;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.Hosting;

namespace GitAgent.Commands
{
    internal class ProvidersCommand
    {
        public static Command BuildProvidersCommand()
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
    }
}
