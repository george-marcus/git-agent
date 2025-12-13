using GitAgent.Models;
using GitAgent.Providers;

namespace GitAgent.Services;

public interface IConflictResolver
{
    Task<ConflictAnalysis> AnalyzeConflictsAsync(RepoContext context);
    Task<List<ConflictResolution>> SuggestResolutionsAsync(RepoContext context, IModelProvider? provider = null);
    Task<List<GeneratedCommand>> GenerateResolutionCommandsAsync(ConflictResolution resolution);
    Task<bool> ApplyResolutionAsync(ConflictResolution resolution);
    Task<bool> ApplyAllResolutionsAsync(List<ConflictResolution> resolutions, string filePath);
}

public class ConflictResolver : IConflictResolver
{
    public Task<ConflictAnalysis> AnalyzeConflictsAsync(RepoContext context)
    {
        var analysis = new ConflictAnalysis
        {
            MergeState = context.MergeState,
            TotalConflicts = context.ConflictedFiles.Sum(f => f.Sections.Count),
            ConflictedFileCount = context.ConflictedFiles.Count,
            MergeMessage = context.MergeMessage
        };

        foreach (var file in context.ConflictedFiles)
        {
            var fileAnalysis = new FileConflictAnalysis
            {
                FilePath = file.FilePath,
                ConflictCount = file.Sections.Count
            };

            foreach (var section in file.Sections)
            {
                var sectionAnalysis = AnalyzeConflictSection(section);
                fileAnalysis.Sections.Add(sectionAnalysis);
            }

            analysis.Files.Add(fileAnalysis);
        }

        return Task.FromResult(analysis);
    }

    private static ConflictSectionAnalysis AnalyzeConflictSection(ConflictSection section)
    {
        var analysis = new ConflictSectionAnalysis
        {
            StartLine = section.StartLine,
            EndLine = section.EndLine,
            OursLabel = section.OursLabel,
            TheirsLabel = section.TheirsLabel
        };

        var oursEmpty = string.IsNullOrWhiteSpace(section.OursContent);
        var theirsEmpty = string.IsNullOrWhiteSpace(section.TheirsContent);

        if (oursEmpty && !theirsEmpty)
        {
            analysis.ConflictType = ConflictType.OursDeleted;
            analysis.Description = "Our side deleted this content, theirs modified it";
        }
        else if (!oursEmpty && theirsEmpty)
        {
            analysis.ConflictType = ConflictType.TheirsDeleted;
            analysis.Description = "Their side deleted this content, ours modified it";
        }
        else if (section.OursContent == section.TheirsContent)
        {
            analysis.ConflictType = ConflictType.SameChange;
            analysis.Description = "Both sides made identical changes";
        }
        else
        {
            var oursLines = section.OursContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var theirsLines = section.TheirsContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (oursLines.Intersect(theirsLines).Any())
            {
                analysis.ConflictType = ConflictType.OverlappingChanges;
                analysis.Description = "Both sides modified the same lines differently";
            }
            else
            {
                analysis.ConflictType = ConflictType.AdjacentChanges;
                analysis.Description = "Changes are adjacent and may be mergeable";
            }
        }

        return analysis;
    }

    public async Task<List<ConflictResolution>> SuggestResolutionsAsync(RepoContext context, IModelProvider? provider = null)
    {
        var resolutions = new List<ConflictResolution>();

        foreach (var file in context.ConflictedFiles)
        {
            var fileExtension = Path.GetExtension(file.FilePath);
            foreach (var section in file.Sections)
            {
                var sectionResolutions = await SuggestSectionResolutionsAsync(file.FilePath, fileExtension, section, provider);
                resolutions.AddRange(sectionResolutions);
            }
        }

        return resolutions;
    }

    private static async Task<List<ConflictResolution>> SuggestSectionResolutionsAsync(
        string filePath, string fileExtension, ConflictSection section, IModelProvider? provider)
    {
        var resolutions = new List<ConflictResolution>();

        resolutions.Add(new ConflictResolution
        {
            FilePath = filePath,
            Section = section,
            Strategy = ResolutionStrategy.AcceptOurs,
            Description = $"Accept our changes ({section.OursLabel})",
            ResolvedContent = section.OursContent
        });

        resolutions.Add(new ConflictResolution
        {
            FilePath = filePath,
            Section = section,
            Strategy = ResolutionStrategy.AcceptTheirs,
            Description = $"Accept their changes ({section.TheirsLabel})",
            ResolvedContent = section.TheirsContent
        });

        if (!string.IsNullOrWhiteSpace(section.OursContent) && !string.IsNullOrWhiteSpace(section.TheirsContent))
        {
            resolutions.Add(new ConflictResolution
            {
                FilePath = filePath,
                Section = section,
                Strategy = ResolutionStrategy.CombineBoth,
                Description = "Combine both changes (ours first, then theirs)",
                ResolvedContent = section.OursContent + "\n" + section.TheirsContent
            });

            if (provider != null)
            {
                try
                {
                    var aiResult = await provider.GenerateConflictResolution(section, filePath, fileExtension);
                    resolutions.Add(new ConflictResolution
                    {
                        FilePath = filePath,
                        Section = section,
                        Strategy = ResolutionStrategy.AiSuggested,
                        Description = $"AI suggestion ({aiResult.Confidence}): {aiResult.Explanation}",
                        ResolvedContent = aiResult.ResolvedContent
                    });
                }
                catch
                {
                    var smartResolution = GetSmartMergeResolution(filePath, section);
                    if (smartResolution != null)
                    {
                        resolutions.Add(smartResolution);
                    }
                }
            }
            else
            {
                var smartResolution = GetSmartMergeResolution(filePath, section);
                if (smartResolution != null)
                {
                    resolutions.Add(smartResolution);
                }
            }
        }

        return resolutions;
    }

    private static ConflictResolution? GetSmartMergeResolution(string filePath, ConflictSection section)
    {
        var smartMergedContent = TrySmartMerge(section);

        var simpleConcat = section.OursContent + "\n" + section.TheirsContent;
        if (smartMergedContent == simpleConcat)
        {
            return null;
        }

        return new ConflictResolution
        {
            FilePath = filePath,
            Section = section,
            Strategy = ResolutionStrategy.AiSuggested,
            Description = "Smart merge (combines unique changes from both sides)",
            ResolvedContent = smartMergedContent
        };
    }

    private static string TrySmartMerge(ConflictSection section)
    {
        var oursLines = section.OursContent.Split('\n').ToList();
        var theirsLines = section.TheirsContent.Split('\n').ToList();

        var common = oursLines.Intersect(theirsLines).ToHashSet();
        var oursUnique = oursLines.Where(l => !common.Contains(l)).ToList();
        var theirsUnique = theirsLines.Where(l => !common.Contains(l)).ToList();

        var result = common.Concat(oursUnique).Concat(theirsUnique);
        return string.Join("\n", result);
    }

    public async Task<bool> ApplyResolutionAsync(ConflictResolution resolution)
    {
        try
        {
            if (!File.Exists(resolution.FilePath))
            {
                return false;
            }

            var content = await File.ReadAllTextAsync(resolution.FilePath);
            var newContent = ReplaceConflictSection(content, resolution.Section, resolution.ResolvedContent);

            if (newContent == content)
            {
                return false; // No change made
            }

            await File.WriteAllTextAsync(resolution.FilePath, newContent);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ApplyAllResolutionsAsync(List<ConflictResolution> resolutions, string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            var content = await File.ReadAllTextAsync(filePath);
            
            var orderedResolutions = resolutions
                .Where(r => r.FilePath == filePath)
                .OrderByDescending(r => r.Section.StartLine)
                .ToList();

            foreach (var resolution in orderedResolutions)
            {
                content = ReplaceConflictSection(content, resolution.Section, resolution.ResolvedContent);
            }

            await File.WriteAllTextAsync(filePath, content);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ReplaceConflictSection(string fileContent, ConflictSection section, string resolvedContent)
    {
        var lines = fileContent.Split('\n').ToList();

        int startIndex = -1;
        int endIndex = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("<<<<<<<") && startIndex == -1)
            {
                if (string.IsNullOrEmpty(section.OursLabel) || lines[i].Contains(section.OursLabel) || i + 1 == section.StartLine)
                {
                    startIndex = i;
                }
            }
            else if (lines[i].StartsWith(">>>>>>>") && startIndex != -1)
            {
                if (string.IsNullOrEmpty(section.TheirsLabel) || lines[i].Contains(section.TheirsLabel) || i + 1 == section.EndLine)
                {
                    endIndex = i;
                    break;
                }
            }
        }

        if (startIndex == -1 || endIndex == -1)
        {
            return fileContent;
        }

        lines.RemoveRange(startIndex, endIndex - startIndex + 1);

        var resolvedLines = resolvedContent.Split('\n');
        lines.InsertRange(startIndex, resolvedLines);

        return string.Join("\n", lines);
    }

    public Task<List<GeneratedCommand>> GenerateResolutionCommandsAsync(ConflictResolution resolution)
    {
        var commands = new List<GeneratedCommand>();
      
        commands.Add(new GeneratedCommand
        {
            CommandText = $"git add \"{resolution.FilePath}\"",
            Risk = "safe",
            Reason = "Stage the resolved file"
        });

        return Task.FromResult(commands);
    }
}

public class ConflictAnalysis
{
    public MergeState MergeState { get; set; }
    public int TotalConflicts { get; set; }
    public int ConflictedFileCount { get; set; }
    public string MergeMessage { get; set; } = "";
    public List<FileConflictAnalysis> Files { get; set; } = [];
}

public class FileConflictAnalysis
{
    public string FilePath { get; set; } = "";
    public int ConflictCount { get; set; }
    public List<ConflictSectionAnalysis> Sections { get; set; } = [];
}

public class ConflictSectionAnalysis
{
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string OursLabel { get; set; } = "";
    public string TheirsLabel { get; set; } = "";
    public ConflictType ConflictType { get; set; }
    public string Description { get; set; } = "";
}

public enum ConflictType
{
    OursDeleted,
    TheirsDeleted,
    SameChange,
    OverlappingChanges,
    AdjacentChanges
}

public class ConflictResolution
{
    public string FilePath { get; set; } = "";
    public ConflictSection Section { get; set; } = new();
    public ResolutionStrategy Strategy { get; set; }
    public string Description { get; set; } = "";
    public string ResolvedContent { get; set; } = "";
}

public enum ResolutionStrategy
{
    AcceptOurs,
    AcceptTheirs,
    CombineBoth,
    AiSuggested,
    Manual
}
