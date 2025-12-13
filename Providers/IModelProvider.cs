using GitAgent.Models;

namespace GitAgent.Providers;

public interface IModelProvider
{
    Task<IReadOnlyList<GeneratedCommand>> GenerateGitCommands(string instruction, RepoContext context);
    Task<ConflictResolutionResult> GenerateConflictResolution(ConflictSection conflict, string filePath, string fileExtension);
}

public class ConflictResolutionResult
{
    public string ResolvedContent { get; set; } = "";
    public string Explanation { get; set; } = "";
    public ResolutionConfidence Confidence { get; set; } = ResolutionConfidence.Low;
}

public enum ResolutionConfidence
{
    Low,
    Medium,
    High
}
