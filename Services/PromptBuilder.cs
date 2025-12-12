using System.Reflection;
using GitAgent.Models;

namespace GitAgent.Services;

public interface IPromptBuilder
{
    string BuildPrompt(string instruction, RepoContext context);
}

public class PromptBuilder : IPromptBuilder
{
    private readonly string _template;

    public PromptBuilder()
    {
        _template = LoadEmbeddedTemplate();
    }

    private static string LoadEmbeddedTemplate()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "GitAgent.PromptTemplate.txt";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return GetFallbackTemplate();
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string GetFallbackTemplate()
    {
        return """
            Translate the user instruction into git commands.
            Rules:
            - Only return lines that are git commands. No explanation.
            - Prefer safe operations (status, add, commit, push, branch, checkout, fetch, pull).
            - If a destructive command is required (reset --hard, clean, force push), ask for explicit confirmation by marking it as DESTRUCTIVE.

            Context:
            - Current branch: {{CurrentBranch}}
            - Staged changes (porcelain):
            {{StatusPorcelain}}
            - Last commit message:
            {{LastCommit}}

            User instruction:
            {{Instruction}}

            Output format:
            One git command per line. If multiple steps are needed, list them in the order they should run.
            """;
    }

    public string BuildPrompt(string instruction, RepoContext context)
    {
        return _template
            .Replace("{{CurrentBranch}}", context.CurrentBranch)
            .Replace("{{StatusPorcelain}}", context.StatusPorcelain)
            .Replace("{{LastCommit}}", context.LastCommit)
            .Replace("{{Remotes}}", context.Remotes)
            .Replace("{{Instruction}}", instruction);
    }
}
