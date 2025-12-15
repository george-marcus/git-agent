using System.CommandLine;

namespace GitAgent.Commands
{
    internal class HelpCommand
    {
        public static Command BuildHelpCommand()
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
}
