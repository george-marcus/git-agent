namespace GitAgent.Models;

public class RepoContext
{
    public string CurrentBranch { get; set; } = "";
    public string StatusPorcelain { get; set; } = "";
    public string LastCommit { get; set; } = "";
    public string Remotes { get; set; } = "";
}
