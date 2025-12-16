using System.CommandLine;

namespace GitAgent.Commands;

public static class CommandsBuilder
{
    public static RootCommand BuildRootCommand()
    {
        var root = new RootCommand("git-agent: Translate natural language to git commands using AI")
        {
            Name = "git-agent"
        };

        root.AddCommand(RunCommand.BuildRunCommand());
        root.AddCommand(ConfigCommand.BuildConfigCommand());
        root.AddCommand(ProvidersCommand.BuildProvidersCommand());
        root.AddCommand(CacheCommand.BuildCacheCommand());
        root.AddCommand(ConflictsCommand.BuildConflictsCommand());
        root.AddCommand(CompletionsCommand.BuildCompletionsCommand());
        root.AddCommand(HelpCommand.BuildHelpCommand());

        return root;
    }
}

