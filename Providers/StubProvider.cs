using GitAgent.Models;

namespace GitAgent.Providers;

public class StubProvider : IModelProvider
{
    public Task<IReadOnlyList<GeneratedCommand>> GenerateGitCommands(string instruction, RepoContext context)
    {
        var list = new List<GeneratedCommand>
        {
            new() { CommandText = "git status" }
        };
        return Task.FromResult((IReadOnlyList<GeneratedCommand>)list);
    }
}
