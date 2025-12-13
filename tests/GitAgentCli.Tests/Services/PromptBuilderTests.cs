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

        var result = _builder.BuildCommandUserPrompt(instruction, context);

        result.Should().Contain("commit all changes");
        result.Should().NotContain("{{Instruction}}");
    }

    [Fact]
    public void BuildPrompt_ReplacesCurrentBranchPlaceholder()
    {
        var context = new RepoContext { CurrentBranch = "main" };

        var result = _builder.BuildCommandUserPrompt("test", context);

        result.Should().Contain("main");
        result.Should().NotContain("{{CurrentBranch}}");
    }

    [Fact]
    public void BuildPrompt_ReplacesStatusPorcelainPlaceholder()
    {
        var context = new RepoContext { StatusPorcelain = "M file.txt\nA new.txt" };

        var result = _builder.BuildCommandUserPrompt("test", context);

        result.Should().Contain("M file.txt");
        result.Should().Contain("A new.txt");
        result.Should().NotContain("{{StatusPorcelain}}");
    }

    [Fact]
    public void BuildPrompt_ReplacesLastCommitPlaceholder()
    {
        var context = new RepoContext { LastCommit = "feat: add new feature" };

        var result = _builder.BuildCommandUserPrompt("test", context);

        result.Should().Contain("feat: add new feature");
        result.Should().NotContain("{{LastCommit}}");
    }

    [Fact]
    public void BuildPrompt_ReplacesRemotesPlaceholder_WhenTemplateContainsIt()
    {
        var context = new RepoContext { Remotes = "origin\thttps://github.com/user/repo.git (fetch)" };

        var result = _builder.BuildCommandUserPrompt("test", context);

        result.Should().NotContain("{{Remotes}}");
    }

    [Fact]
    public void BuildPrompt_WithEmptyContext_ReplacesWithEmptyStrings()
    {
        var context = new RepoContext();

        var result = _builder.BuildCommandUserPrompt("test", context);

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

        var result = _builder.BuildCommandUserPrompt(instruction, context);

        result.Should().Contain("feature/test");
        result.Should().Contain("M README.md");
        result.Should().Contain("initial commit");
        result.Should().Contain("push changes to remote");
    }

    [Fact]
    public void BuildPrompt_ContainsGitCommandGuidelines()
    {
        var context = new RepoContext();

        var result = _builder.BuildCommandUserPrompt("test", context);

        result.Should().Contain("git");
    }

    [Fact]
    public void BuildPrompt_WithSpecialCharactersInInstruction_PreservesCharacters()
    {
        var context = new RepoContext();
        var instruction = "commit with message \"feat: add 'quotes' & special <chars>\"";

        var result = _builder.BuildCommandUserPrompt(instruction, context);

        result.Should().Contain(instruction);
    }

    [Fact]
    public void BuildPrompt_WithMultilineStatus_PreservesNewlines()
    {
        var context = new RepoContext
        {
            StatusPorcelain = "M file1.txt\nA file2.txt\nD file3.txt"
        };

        var result = _builder.BuildCommandUserPrompt("test", context);

        result.Should().Contain("M file1.txt");
        result.Should().Contain("A file2.txt");
        result.Should().Contain("D file3.txt");
    }

    [Fact]
    public void BuildConflictUserPrompt_IncludesFileInfo()
    {
        var conflict = new ConflictSection
        {
            StartLine = 10,
            EndLine = 20,
            OursLabel = "HEAD",
            TheirsLabel = "feature-branch",
            OursContent = "our code",
            TheirsContent = "their code"
        };

        var result = _builder.BuildConflictUserPrompt(conflict, "src/file.cs", ".cs");

        result.Should().Contain("src/file.cs");
        result.Should().Contain(".cs");
        result.Should().Contain("10");
        result.Should().Contain("20");
    }

    [Fact]
    public void BuildConflictUserPrompt_IncludesOursContent()
    {
        var conflict = new ConflictSection
        {
            OursLabel = "HEAD",
            TheirsLabel = "feature",
            OursContent = "public void OurMethod() { }",
            TheirsContent = "their code"
        };

        var result = _builder.BuildConflictUserPrompt(conflict, "file.cs", ".cs");

        result.Should().Contain("OUR CHANGES");
        result.Should().Contain("public void OurMethod() { }");
        result.Should().Contain("HEAD");
    }

    [Fact]
    public void BuildConflictUserPrompt_IncludesTheirsContent()
    {
        var conflict = new ConflictSection
        {
            OursLabel = "HEAD",
            TheirsLabel = "feature-branch",
            OursContent = "our code",
            TheirsContent = "public void TheirMethod() { }"
        };

        var result = _builder.BuildConflictUserPrompt(conflict, "file.cs", ".cs");

        result.Should().Contain("THEIR CHANGES");
        result.Should().Contain("public void TheirMethod() { }");
        result.Should().Contain("feature-branch");
    }

    [Fact]
    public void BuildConflictUserPrompt_IncludesBaseContent_WhenProvided()
    {
        var conflict = new ConflictSection
        {
            OursLabel = "HEAD",
            TheirsLabel = "feature",
            OursContent = "our code",
            TheirsContent = "their code",
            BaseContent = "original code"
        };

        var result = _builder.BuildConflictUserPrompt(conflict, "file.cs", ".cs");

        result.Should().Contain("BASE");
        result.Should().Contain("original code");
    }

    [Fact]
    public void BuildConflictUserPrompt_OmitsBaseContent_WhenEmpty()
    {
        var conflict = new ConflictSection
        {
            OursLabel = "HEAD",
            TheirsLabel = "feature",
            OursContent = "our code",
            TheirsContent = "their code",
            BaseContent = ""
        };

        var result = _builder.BuildConflictUserPrompt(conflict, "file.cs", ".cs");

        result.Should().NotContain("BASE (common ancestor)");
    }

    [Fact]
    public void BuildConflictUserPrompt_ContainsMergeInstruction()
    {
        var conflict = new ConflictSection
        {
            OursContent = "our code",
            TheirsContent = "their code"
        };

        var result = _builder.BuildConflictUserPrompt(conflict, "file.cs", ".cs");

        result.Should().Contain("analyze");
        result.Should().Contain("resolution");
    }
}

public class OllamaPromptBuilderTests
{
    private readonly OllamaPromptBuilder _builder = new();

    [Fact]
    public void BuildConflictUserPrompt_ContainsResolvedCodeMarker()
    {
        var conflict = new ConflictSection
        {
            OursContent = "our code",
            TheirsContent = "their code"
        };

        var result = _builder.BuildConflictUserPrompt(conflict, "file.cs", ".cs");

        result.Should().Contain("RESOLVED_CODE:");
    }

    [Fact]
    public void BuildConflictUserPrompt_ContainsExplanationMarker()
    {
        var conflict = new ConflictSection
        {
            OursContent = "our code",
            TheirsContent = "their code"
        };

        var result = _builder.BuildConflictUserPrompt(conflict, "file.cs", ".cs");

        result.Should().Contain("EXPLANATION:");
    }

    [Fact]
    public void BuildConflictUserPrompt_ContainsConfidenceMarker()
    {
        var conflict = new ConflictSection
        {
            OursContent = "our code",
            TheirsContent = "their code"
        };

        var result = _builder.BuildConflictUserPrompt(conflict, "file.cs", ".cs");

        result.Should().Contain("CONFIDENCE:");
    }

    [Fact]
    public void BuildConflictUserPrompt_IncludesFileInfo()
    {
        var conflict = new ConflictSection
        {
            StartLine = 15,
            EndLine = 25,
            OursContent = "our code",
            TheirsContent = "their code"
        };

        var result = _builder.BuildConflictUserPrompt(conflict, "src/app.ts", ".ts");

        result.Should().Contain("src/app.ts");
        result.Should().Contain(".ts");
        result.Should().Contain("15");
        result.Should().Contain("25");
    }

    [Fact]
    public void BuildConflictUserPrompt_IncludesOursAndTheirsContent()
    {
        var conflict = new ConflictSection
        {
            OursLabel = "main",
            TheirsLabel = "feature/new",
            OursContent = "function ourVersion() {}",
            TheirsContent = "function theirVersion() {}"
        };

        var result = _builder.BuildConflictUserPrompt(conflict, "file.js", ".js");

        result.Should().Contain("function ourVersion() {}");
        result.Should().Contain("function theirVersion() {}");
        result.Should().Contain("main");
        result.Should().Contain("feature/new");
    }

    [Fact]
    public void BuildConflictUserPrompt_IncludesBaseContent_WhenProvided()
    {
        var conflict = new ConflictSection
        {
            OursContent = "our code",
            TheirsContent = "their code",
            BaseContent = "base code"
        };

        var result = _builder.BuildConflictUserPrompt(conflict, "file.cs", ".cs");

        result.Should().Contain("BASE");
        result.Should().Contain("base code");
    }

    [Fact]
    public void BuildConflictUserPrompt_OmitsBaseContent_WhenEmpty()
    {
        var conflict = new ConflictSection
        {
            OursContent = "our code",
            TheirsContent = "their code",
            BaseContent = ""
        };

        var result = _builder.BuildConflictUserPrompt(conflict, "file.cs", ".cs");

        result.Should().NotContain("common ancestor");
    }

    [Fact]
    public void BuildConflictUserPrompt_ContainsExampleFormat()
    {
        var conflict = new ConflictSection
        {
            OursContent = "our code",
            TheirsContent = "their code"
        };

        var result = _builder.BuildConflictUserPrompt(conflict, "file.cs", ".cs");

        result.Should().Contain("Example format:");
        result.Should().Contain("your merged code here");
    }

    [Fact]
    public void BuildConflictUserPrompt_InheritsCommandPromptFromBase()
    {
        var context = new RepoContext { CurrentBranch = "main" };

        var result = _builder.BuildCommandUserPrompt("commit all", context);

        result.Should().Contain("commit all");
        result.Should().Contain("main");
    }
}
