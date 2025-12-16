using System.Diagnostics;
using System.Text.RegularExpressions;
using GitAgent.Models;

namespace GitAgent.Services.Git;

public interface IGitInspector
{
    Task<RepoContext> BuildRepoContextAsync();
    Task<MergeState> DetectMergeStateAsync();
    Task<List<ConflictedFile>> GetConflictedFilesAsync();
    Task<List<ConflictSection>> ParseConflictMarkersAsync(string filePath);
}

public partial class GitInspector : IGitInspector
{
    private static async Task<string> RunGitAsync(string args)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi);
        if (p == null)
        {
            return "";
        }

        var output = await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();
        return output.Trim();
    }

    private static async Task<bool> FileExistsInGitDirAsync(string fileName)
    {
        var gitDir = await RunGitAsync("rev-parse --git-dir");
        if (string.IsNullOrEmpty(gitDir)) return false;
        return File.Exists(Path.Combine(gitDir, fileName));
    }

    public async Task<RepoContext> BuildRepoContextAsync()
    {
        var ctx = new RepoContext();
        try
        {
            ctx.CurrentBranch = await RunGitAsync("rev-parse --abbrev-ref HEAD");
            ctx.StatusPorcelain = await RunGitAsync("status --porcelain");
            ctx.LastCommit = await RunGitAsync("log -1 --pretty=%B");
            ctx.Remotes = await RunGitAsync("remote -v");

            ctx.MergeState = await DetectMergeStateAsync();
            if (ctx.MergeState != MergeState.None)
            {
                ctx.ConflictedFiles = await GetConflictedFilesAsync();
                ctx.MergeHead = await RunGitAsync("rev-parse MERGE_HEAD");
                ctx.MergeMessage = await GetMergeMessageAsync();
            }
        }
        catch
        {
            // Not a git repo or git is not installed
        }
        return ctx;
    }

    public async Task<MergeState> DetectMergeStateAsync()
    {
        try
        {
            if (await FileExistsInGitDirAsync("MERGE_HEAD"))
            {
                return MergeState.Merging;
            }
            if (await FileExistsInGitDirAsync("rebase-merge") || await FileExistsInGitDirAsync("rebase-apply"))
            {
                return MergeState.Rebasing;
            }
            if (await FileExistsInGitDirAsync("CHERRY_PICK_HEAD"))
            {
                return MergeState.CherryPicking;
            }
            if (await FileExistsInGitDirAsync("REVERT_HEAD"))
            {
                return MergeState.Reverting;
            }
        }
        catch
        {
            // Ignore errors
        }
        return MergeState.None;
    }

    public async Task<List<ConflictedFile>> GetConflictedFilesAsync()
    {
        var conflicts = new List<ConflictedFile>();
        try
        {
            var status = await RunGitAsync("status --porcelain");
            var lines = status.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // UU = both modified (conflict)
                // AA = both added
                // DD = both deleted
                // AU/UA, DU/UD = various conflict states
                if (line.Length >= 2)
                {
                    var xy = line[..2];
                    if (xy is "UU" or "AA" or "DD" or "AU" or "UA" or "DU" or "UD")
                    {
                        var filePath = line[3..].Trim();
                        var conflictedFile = new ConflictedFile { FilePath = filePath };

                        // Parse conflict markers if file exists
                        if (File.Exists(filePath))
                        {
                            conflictedFile.Sections = await ParseConflictMarkersAsync(filePath);
                        }

                        conflicts.Add(conflictedFile);
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        return conflicts;
    }

    public async Task<List<ConflictSection>> ParseConflictMarkersAsync(string filePath)
    {
        var sections = new List<ConflictSection>();
        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            var lines = content.Split('\n');

            ConflictSection? current = null;
            var oursBuilder = new List<string>();
            var theirsBuilder = new List<string>();
            var baseBuilder = new List<string>();
            var state = ConflictParseState.Outside;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.StartsWith("<<<<<<<"))
                {
                    current = new ConflictSection
                    {
                        StartLine = i + 1,
                        OursLabel = line.Length > 8 ? line[8..].Trim() : ""
                    };
                    state = ConflictParseState.Ours;
                    oursBuilder.Clear();
                    baseBuilder.Clear();
                    theirsBuilder.Clear();
                }
                else if (line.StartsWith("|||||||") && current != null)
                {
                    // diff3 style - base content
                    state = ConflictParseState.Base;
                }
                else if (line.StartsWith("=======") && current != null)
                {
                    state = ConflictParseState.Theirs;
                }
                else if (line.StartsWith(">>>>>>>") && current != null)
                {
                    current.EndLine = i + 1;
                    current.OursContent = string.Join("\n", oursBuilder);
                    current.BaseContent = string.Join("\n", baseBuilder);
                    current.TheirsContent = string.Join("\n", theirsBuilder);
                    current.TheirsLabel = line.Length > 8 ? line[8..].Trim() : "";
                    sections.Add(current);
                    current = null;
                    state = ConflictParseState.Outside;
                }
                else if (current != null)
                {
                    switch (state)
                    {
                        case ConflictParseState.Ours:
                            oursBuilder.Add(line);
                            break;
                        case ConflictParseState.Base:
                            baseBuilder.Add(line);
                            break;
                        case ConflictParseState.Theirs:
                            theirsBuilder.Add(line);
                            break;
                    }
                }
            }
        }
        catch
        {
            // Ignore file read errors
        }
        return sections;
    }

    private static async Task<string> GetMergeMessageAsync()
    {
        try
        {
            var gitDir = await RunGitAsync("rev-parse --git-dir");
            var mergeMsgPath = Path.Combine(gitDir, "MERGE_MSG");
            if (File.Exists(mergeMsgPath))
            {
                return await File.ReadAllTextAsync(mergeMsgPath);
            }
        }
        catch
        {
            // Ignore errors
        }
        return "";
    }

    private enum ConflictParseState
    {
        Outside,
        Ours,
        Base,
        Theirs
    }
}
