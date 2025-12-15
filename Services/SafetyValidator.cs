using GitAgent.Models;

namespace GitAgent.Services;

public interface ISafetyValidator
{
    IReadOnlyList<GeneratedCommand> FilterAndAnnotate(IReadOnlyList<GeneratedCommand> commands);
}

public class SafetyValidator : ISafetyValidator
{
    private static readonly string[] SafePrefixes =
    [
        "git status",
        "git add",
        "git commit",
        "git push",
        "git pull",
        "git branch",
        "git checkout",
        "git switch",
        "git merge",
        "git fetch",
        "git reset --soft",
        "git log",
        "git diff",
        "git stash",
        "git tag",
        "git remote",
        "git show",
        "git rebase"
    ];

    public IReadOnlyList<GeneratedCommand> FilterAndAnnotate(IReadOnlyList<GeneratedCommand> commands)
    {
        var result = new List<GeneratedCommand>();

        foreach (var cmd in commands)
        {
            var text = cmd.CommandText.Trim();
            var providerRisk = cmd.Risk;

            if (IsDestructive(text))
            {
                cmd.Risk = "destructive";
                cmd.Reason ??= GetDestructiveReason(text);
                result.Add(cmd);
            }
            else if (IsSafe(text))
            {
                cmd.Risk = GetHigherRisk(providerRisk, "safe");
                result.Add(cmd);
            }
            else
            {
                cmd.Risk = GetHigherRisk(providerRisk, "unknown");
                cmd.Reason ??= "Not in allowlist; requires manual review";
                if (text.StartsWith("git ", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(cmd);
                }
            }
        }

        return result;
    }

    private static string GetHigherRisk(string providerRisk, string validatorRisk)
    {
        var riskLevel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["safe"] = 0,
            ["unknown"] = 1,
            ["moderate"] = 2,
            ["destructive"] = 3
        };

        var providerLevel = riskLevel.GetValueOrDefault(providerRisk, 1);
        var validatorLevel = riskLevel.GetValueOrDefault(validatorRisk, 1);

        return providerLevel >= validatorLevel ? providerRisk : validatorRisk;
    }

    private static bool IsSafe(string command)
    {
        return SafePrefixes.Any(prefix => command.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDestructive(string command)
    {
        var lower = command.ToLowerInvariant();

        var hasForceDeleteBranch = command.Contains("branch -D", StringComparison.Ordinal);

        return lower.Contains("--force") ||
               lower.Contains("-f ") ||
               lower.Contains("reset --hard") ||
               lower.Contains("git clean") ||
               lower.Contains("push --delete") ||
               hasForceDeleteBranch ||
               (lower.Contains("branch -d") && lower.Contains("--force"));
    }

    private static string GetDestructiveReason(string command)
    {
        var lower = command.ToLowerInvariant();

        if (lower.Contains("reset --hard"))
        {
            return "Hard reset will discard uncommitted changes";
        }

        if (lower.Contains("--force") || lower.Contains("-f "))
        {
            return "Force operation may overwrite remote history";
        }

        if (lower.Contains("git clean"))
        {
            return "Clean will permanently delete untracked files";
        }

        if (lower.Contains("push --delete") || command.Contains("branch -D", StringComparison.Ordinal))
        {
            return "Will permanently delete branch";
        }

        return "Destructive operation detected";
    }
}
