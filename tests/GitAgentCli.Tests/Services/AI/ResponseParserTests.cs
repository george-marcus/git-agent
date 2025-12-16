using FluentAssertions;
using GitAgent.Services.AI;

namespace GitAgentCli.Tests.Services.AI;

public class ResponseParserTests
{
    private readonly ResponseParser _parser = new();

    [Fact]
    public void ParseResponse_WithEmptyString_ReturnsEmptyList()
    {
        var result = _parser.ParseResponse("");
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseResponse_WithWhitespace_ReturnsEmptyList()
    {
        var result = _parser.ParseResponse("   \n\t  ");
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseResponse_WithSingleGitCommand_ReturnsSingleCommand()
    {
        var result = _parser.ParseResponse("git status");

        result.Should().HaveCount(1);
        result[0].CommandText.Should().Be("git status");
        result[0].Risk.Should().Be("unknown");
    }

    [Fact]
    public void ParseResponse_WithMultipleGitCommands_ReturnsAllCommands()
    {
        var response = """
            git add .
            git commit -m "test"
            git push
            """;

        var result = _parser.ParseResponse(response);

        result.Should().HaveCount(3);
        result[0].CommandText.Should().Be("git add .");
        result[1].CommandText.Should().Be("git commit -m \"test\"");
        result[2].CommandText.Should().Be("git push");
    }

    [Fact]
    public void ParseResponse_WithCodeFences_IgnoresCodeFenceLines()
    {
        var response = """
            ```bash
            git status
            ```
            """;

        var result = _parser.ParseResponse(response);

        result.Should().HaveCount(1);
        result[0].CommandText.Should().Be("git status");
    }

    [Fact]
    public void ParseResponse_WithComments_IgnoresCommentLines()
    {
        var response = """
            # This is a comment
            git status
            // Another comment
            git add .
            * bullet point
            """;

        var result = _parser.ParseResponse(response);

        result.Should().HaveCount(2);
        result[0].CommandText.Should().Be("git status");
        result[1].CommandText.Should().Be("git add .");
    }

    [Fact]
    public void ParseResponse_WithNumberedList_ExtractsCommands()
    {
        var response = """
            1. git add .
            2. git commit -m "test"
            3. git push
            """;

        var result = _parser.ParseResponse(response);

        result.Should().HaveCount(3);
        result[0].CommandText.Should().Be("git add .");
        result[1].CommandText.Should().Be("git commit -m \"test\"");
        result[2].CommandText.Should().Be("git push");
    }

    [Fact]
    public void ParseResponse_WithBulletList_ExtractsCommands()
    {
        var response = """
            - git add .
            - git commit -m "test"
            • git push
            """;

        var result = _parser.ParseResponse(response);

        result.Should().HaveCount(3);
        result[0].CommandText.Should().Be("git add .");
        result[1].CommandText.Should().Be("git commit -m \"test\"");
        result[2].CommandText.Should().Be("git push");
    }

    [Fact]
    public void ParseResponse_WithNonGitCommands_FiltersThemOut()
    {
        var response = """
            git status
            echo "hello"
            ls -la
            git add .
            """;

        var result = _parser.ParseResponse(response);

        result.Should().HaveCount(2);
        result[0].CommandText.Should().Be("git status");
        result[1].CommandText.Should().Be("git add .");
    }

    [Fact]
    public void ParseResponse_WithMixedContent_ExtractsOnlyGitCommands()
    {
        var response = """
            Here are the commands you need:

            ```bash
            git add .
            git commit -m "feat: add new feature"
            ```

            Then push your changes:
            git push origin main
            """;

        var result = _parser.ParseResponse(response);

        result.Should().HaveCount(3);
        result[0].CommandText.Should().Be("git add .");
        result[1].CommandText.Should().Be("git commit -m \"feat: add new feature\"");
        result[2].CommandText.Should().Be("git push origin main");
    }

    [Theory]
    [InlineData("git status", "git status")]
    [InlineData("  git status  ", "git status")]
    [InlineData("GIT STATUS", "GIT STATUS")]
    [InlineData("Git Status", "Git Status")]
    public void ParseResponse_TrimsWhitespaceButPreservesCase(string input, string expected)
    {
        var result = _parser.ParseResponse(input);

        result.Should().HaveCount(1);
        result[0].CommandText.Should().Be(expected);
    }
}
