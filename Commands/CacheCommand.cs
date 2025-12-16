using GitAgent.Services.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.Hosting;

namespace GitAgent.Commands
{
    internal class CacheCommand
    {
        public static Command BuildCacheCommand()
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
    }
}
