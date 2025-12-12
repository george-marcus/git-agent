using System.Diagnostics;
using GitAgent.Models;

namespace GitAgent.Services;

public interface IGitInspector
{
    Task<RepoContext> BuildRepoContextAsync();
}

public class GitInspector : IGitInspector
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

    public async Task<RepoContext> BuildRepoContextAsync()
    {
        var ctx = new RepoContext();
        try
        {
            ctx.CurrentBranch = await RunGitAsync("rev-parse --abbrev-ref HEAD");
            ctx.StatusPorcelain = await RunGitAsync("status --porcelain");
            ctx.LastCommit = await RunGitAsync("log -1 --pretty=%B");
            ctx.Remotes = await RunGitAsync("remote -v");
        }
        catch
        {
            // Not a git repo or git is not installed
        }
        return ctx;
    }
}
