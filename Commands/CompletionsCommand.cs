using GitAgent.Services;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.Hosting;

namespace GitAgent.Commands
{
    internal class CompletionsCommand
    {
        public static Command BuildCompletionsCommand()
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
    }
}
