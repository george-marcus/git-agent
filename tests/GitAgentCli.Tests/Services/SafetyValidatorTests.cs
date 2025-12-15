using FluentAssertions;
using GitAgent.Models;
using GitAgent.Services;

namespace GitAgentCli.Tests.Services;

public class SafetyValidatorTests
{
    private readonly SafetyValidator _validator = new();

    private static GeneratedCommand CreateCommand(string text, string risk = "safe") => new()
    {
        CommandText = text,
        Risk = risk
    };

    private static List<GeneratedCommand> CreateCommands(params string[] texts) =>
        texts.Select(t => CreateCommand(t)).ToList();

    [Fact]
    public void FilterAndAnnotate_WithEmptyList_ReturnsEmptyList()
    {
        var result = _validator.FilterAndAnnotate([]);

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("git status")]
    [InlineData("git add .")]
    [InlineData("git add -A")]
    [InlineData("git commit -m \"test\"")]
    [InlineData("git push")]
    [InlineData("git push origin main")]
    [InlineData("git pull")]
    [InlineData("git pull origin main")]
    [InlineData("git branch")]
    [InlineData("git branch feature")]
    [InlineData("git checkout main")]
    [InlineData("git checkout -b feature")]
    [InlineData("git switch main")]
    [InlineData("git merge feature")]
    [InlineData("git fetch")]
    [InlineData("git fetch origin")]
    [InlineData("git reset --soft HEAD~1")]
    [InlineData("git log")]
    [InlineData("git log --oneline")]
    [InlineData("git diff")]
    [InlineData("git diff HEAD")]
    [InlineData("git stash")]
    [InlineData("git stash pop")]
    [InlineData("git tag v1.0.0")]
    [InlineData("git remote -v")]
    [InlineData("git show HEAD")]
    [InlineData("git rebase main")]
    public void FilterAndAnnotate_SafeCommands_MarkedAsSafe(string command)
    {
        var commands = CreateCommands(command);

        var result = _validator.FilterAndAnnotate(commands);

        result.Should().HaveCount(1);
        result[0].Risk.Should().Be("safe");
        result[0].Reason.Should().BeNull();
    }

    [Theory]
    [InlineData("git push --force", "Force operation may overwrite remote history")]
    [InlineData("git push -f origin main", "Force operation may overwrite remote history")]
    [InlineData("git reset --hard HEAD~1", "Hard reset will discard uncommitted changes")]
    [InlineData("git clean -fd", "Clean will permanently delete untracked files")]
    [InlineData("git clean -fdx", "Clean will permanently delete untracked files")]
    [InlineData("git push --delete origin feature", "Will permanently delete branch")]
    [InlineData("git branch -D feature", "Will permanently delete branch")]
    public void FilterAndAnnotate_DestructiveCommands_MarkedAsDestructive(string command, string expectedReason)
    {
        var commands = CreateCommands(command);

        var result = _validator.FilterAndAnnotate(commands);

        result.Should().HaveCount(1);
        result[0].Risk.Should().Be("destructive");
        result[0].Reason.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData("git bisect start")]
    [InlineData("git cherry-pick abc123")]
    [InlineData("git reflog")]
    [InlineData("git blame file.txt")]
    public void FilterAndAnnotate_UnknownGitCommands_MarkedAsUnknown(string command)
    {
        var commands = new List<GeneratedCommand> { CreateCommand(command, "safe") };

        var result = _validator.FilterAndAnnotate(commands);

        result.Should().HaveCount(1);
        result[0].Risk.Should().Be("unknown");
        result[0].Reason.Should().Be("Not in allowlist; requires manual review");
    }

    [Fact]
    public void FilterAndAnnotate_NonGitCommands_AreFiltered()
    {
        var commands = new List<GeneratedCommand>
        {
            CreateCommand("echo hello"),
            CreateCommand("ls -la"),
            CreateCommand("rm -rf /")
        };

        var result = _validator.FilterAndAnnotate(commands);

        result.Should().BeEmpty();
    }

    [Fact]
    public void FilterAndAnnotate_MixedCommands_CorrectlyClassifies()
    {
        var commands = CreateCommands(
            "git status",
            "git push --force",
            "git bisect start",
            "echo hello"
        );

        var result = _validator.FilterAndAnnotate(commands);

        result.Should().HaveCount(3);
        result[0].Risk.Should().Be("safe");
        result[1].Risk.Should().Be("destructive");
        result[2].Risk.Should().Be("unknown");
    }

    [Fact]
    public void FilterAndAnnotate_CaseInsensitive_ForSafetyChecks()
    {
        var commands = CreateCommands(
            "GIT STATUS",
            "Git Push --Force"
        );

        var result = _validator.FilterAndAnnotate(commands);

        result.Should().HaveCount(2);
        result[0].Risk.Should().Be("safe");
        result[1].Risk.Should().Be("destructive");
    }

    [Fact]
    public void FilterAndAnnotate_PreservesOriginalCommandText()
    {
        var commands = CreateCommands("  git status  ");

        var result = _validator.FilterAndAnnotate(commands);

        result.Should().HaveCount(1);
        result[0].CommandText.Should().Be("  git status  ");
    }

    [Fact]
    public void FilterAndAnnotate_MultipleDestructivePatterns_GetsFirstMatchingReason()
    {
        var commands = CreateCommands("git push --force --delete origin branch");

        var result = _validator.FilterAndAnnotate(commands);

        result.Should().HaveCount(1);
        result[0].Risk.Should().Be("destructive");
        result[0].Reason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void FilterAndAnnotate_PreservesProviderRisk_WhenHigherThanValidator()
    {
        // Provider marked a safe command as destructive - validator should preserve it
        var commands = new List<GeneratedCommand>
        {
            CreateCommand("git status", "destructive")
        };

        var result = _validator.FilterAndAnnotate(commands);

        result.Should().HaveCount(1);
        result[0].Risk.Should().Be("destructive");
    }

    [Fact]
    public void FilterAndAnnotate_PreservesProviderRisk_ModerateOnSafeCommand()
    {
        // Provider marked a safe command as moderate - validator should preserve it
        var commands = new List<GeneratedCommand>
        {
            CreateCommand("git commit -m \"test\"", "moderate")
        };

        var result = _validator.FilterAndAnnotate(commands);

        result.Should().HaveCount(1);
        result[0].Risk.Should().Be("moderate");
    }

    [Fact]
    public void FilterAndAnnotate_ValidatorEscalates_WhenProviderUnderestimates()
    {
        // Provider said safe but validator detects destructive pattern
        var commands = new List<GeneratedCommand>
        {
            CreateCommand("git reset --hard HEAD", "safe")
        };

        var result = _validator.FilterAndAnnotate(commands);

        result.Should().HaveCount(1);
        result[0].Risk.Should().Be("destructive");
    }

    [Fact]
    public void FilterAndAnnotate_PreservesProviderReason_WhenProviderRiskIsHigher()
    {
        var commands = new List<GeneratedCommand>
        {
            new() { CommandText = "git status", Risk = "destructive", Reason = "Provider's custom reason" }
        };

        var result = _validator.FilterAndAnnotate(commands);

        result.Should().HaveCount(1);
        result[0].Reason.Should().Be("Provider's custom reason");
    }
}
