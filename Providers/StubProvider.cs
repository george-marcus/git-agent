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

    public Task<ConflictResolutionResult> GenerateConflictResolution(ConflictSection conflict, string filePath, string fileExtension)
    {
        // Stub provider just combines both sides
        var resolvedContent = string.IsNullOrWhiteSpace(conflict.OursContent)
            ? conflict.TheirsContent
            : string.IsNullOrWhiteSpace(conflict.TheirsContent)
                ? conflict.OursContent
                : $"{conflict.OursContent}\n{conflict.TheirsContent}";

        return Task.FromResult(new ConflictResolutionResult
        {
            ResolvedContent = resolvedContent,
            Explanation = "Stub provider: combined both sides",
            Confidence = ResolutionConfidence.Low
        });
    }
}
