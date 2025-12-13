namespace GitAgent.Models;

public class RepoContext
{
    public string CurrentBranch { get; set; } = "";
    public string StatusPorcelain { get; set; } = "";
    public string LastCommit { get; set; } = "";
    public string Remotes { get; set; } = "";
    public MergeState MergeState { get; set; } = MergeState.None;
    public List<ConflictedFile> ConflictedFiles { get; set; } = [];
    public string MergeHead { get; set; } = "";
    public string MergeMessage { get; set; } = "";
}

public enum MergeState
{
    None,
    Merging,
    Rebasing,
    CherryPicking,
    Reverting
}

public class ConflictedFile
{
    public string FilePath { get; set; } = "";
    public List<ConflictSection> Sections { get; set; } = [];
}

public class ConflictSection
{
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string OursContent { get; set; } = "";
    public string TheirsContent { get; set; } = "";
    public string BaseContent { get; set; } = "";
    public string OursLabel { get; set; } = "";
    public string TheirsLabel { get; set; } = "";
}
