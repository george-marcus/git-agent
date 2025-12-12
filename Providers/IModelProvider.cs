using GitAgent.Models;

namespace GitAgent.Providers;

public interface IModelProvider
{
    Task<IReadOnlyList<GeneratedCommand>> GenerateGitCommands(string instruction, RepoContext context);
}
