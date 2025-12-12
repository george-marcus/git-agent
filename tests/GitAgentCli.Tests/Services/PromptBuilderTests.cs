using FluentAssertions;
using GitAgent.Models;
using GitAgent.Services;

namespace GitAgentCli.Tests.Services;

public class PromptBuilderTests
{
    private readonly PromptBuilder _builder = new();

    [Fact]
    public void BuildPrompt_ReplacesInstructionPlaceholder()
    {
        var context = new RepoContext();
        var instruction = "commit all changes";

        var result = _builder.BuildPrompt(instruction, context);

        result.Should().Contain("commit all changes");
        result.Should().NotContain("{{Instruction}}");
    }

    [Fact]
    public void BuildPrompt_ReplacesCurrentBranchPlaceholder()
    {
        var context = new RepoContext { CurrentBranch = "main" };

        var result = _builder.BuildPrompt("test", context);

        result.Should().Contain("main");
        result.Should().NotContain("{{CurrentBranch}}");
    }

    [Fact]
    public void BuildPrompt_ReplacesStatusPorcelainPlaceholder()
    {
        var context = new RepoContext { StatusPorcelain = "M file.txt\nA new.txt" };

        var result = _builder.BuildPrompt("test", context);

        result.Should().Contain("M file.txt");
        result.Should().Contain("A new.txt");
        result.Should().NotContain("{{StatusPorcelain}}");
    }

    [Fact]
    public void BuildPrompt_ReplacesLastCommitPlaceholder()
    {
        var context = new RepoContext { LastCommit = "feat: add new feature" };

        var result = _builder.BuildPrompt("test", context);

        result.Should().Contain("feat: add new feature");
        result.Should().NotContain("{{LastCommit}}");
    }

    [Fact]
    public void BuildPrompt_ReplacesRemotesPlaceholder_WhenTemplateContainsIt()
    {
        var context = new RepoContext { Remotes = "origin\thttps://github.com/user/repo.git (fetch)" };

        var result = _builder.BuildPrompt("test", context);

        result.Should().NotContain("{{Remotes}}");
    }

    [Fact]
    public void BuildPrompt_WithEmptyContext_ReplacesWithEmptyStrings()
    {
        var context = new RepoContext();

        var result = _builder.BuildPrompt("test", context);

        result.Should().NotContain("{{CurrentBranch}}");
        result.Should().NotContain("{{StatusPorcelain}}");
        result.Should().NotContain("{{LastCommit}}");
        result.Should().NotContain("{{Remotes}}");
        result.Should().NotContain("{{Instruction}}");
    }

    [Fact]
    public void BuildPrompt_WithFullContext_IncludesAllValues()
    {
        var context = new RepoContext
        {
            CurrentBranch = "feature/test",
            StatusPorcelain = "M README.md",
            LastCommit = "initial commit",
            Remotes = "origin\tgit@github.com:user/repo.git"
        };
        var instruction = "push changes to remote";

        var result = _builder.BuildPrompt(instruction, context);

        result.Should().Contain("feature/test");
        result.Should().Contain("M README.md");
        result.Should().Contain("initial commit");
        result.Should().Contain("push changes to remote");
    }

    [Fact]
    public void BuildPrompt_ContainsGitCommandGuidelines()
    {
        var context = new RepoContext();

        var result = _builder.BuildPrompt("test", context);

        result.Should().Contain("git");
    }

    [Fact]
    public void BuildPrompt_WithSpecialCharactersInInstruction_PreservesCharacters()
    {
        var context = new RepoContext();
        var instruction = "commit with message \"feat: add 'quotes' & special <chars>\"";

        var result = _builder.BuildPrompt(instruction, context);

        result.Should().Contain(instruction);
    }

    [Fact]
    public void BuildPrompt_WithMultilineStatus_PreservesNewlines()
    {
        var context = new RepoContext
        {
            StatusPorcelain = "M file1.txt\nA file2.txt\nD file3.txt"
        };

        var result = _builder.BuildPrompt("test", context);

        result.Should().Contain("M file1.txt");
        result.Should().Contain("A file2.txt");
        result.Should().Contain("D file3.txt");
    }
}
