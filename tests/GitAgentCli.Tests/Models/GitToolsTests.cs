using System.Text.Json;
using FluentAssertions;
using GitAgent.Models;

namespace GitAgentCli.Tests.Models;

public class GitToolsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    [Fact]
    public void ToolName_ReturnsExpectedValue()
    {
        GitTools.ToolName.Should().Be("execute_git_commands");
    }

    [Fact]
    public void ToolDescription_ContainsKeyInformation()
    {
        GitTools.ToolDescription.Should().Contain("git commands");
        GitTools.ToolDescription.Should().Contain("destructive");
    }

    [Fact]
    public void GetInputSchema_ReturnsValidSchema()
    {
        var schema = GitTools.GetInputSchema();

        schema.Should().NotBeNull();
        var json = JsonSerializer.Serialize(schema);
        json.Should().Contain("commands");
        json.Should().Contain("array");
        json.Should().Contain("command");
        json.Should().Contain("risk");
        json.Should().Contain("reason");
    }

    [Fact]
    public void GetInputSchema_HasRequiredFields()
    {
        var schema = GitTools.GetInputSchema();
        var json = JsonSerializer.Serialize(schema);

        json.Should().Contain("\"required\"");
        json.Should().Contain("\"commands\"");
    }
}

public class GitToolInputTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    [Fact]
    public void Deserialize_WithValidJson_ReturnsGitToolInput()
    {
        var json = """
            {
                "commands": [
                    {
                        "command": "git status",
                        "risk": "safe",
                        "reason": "Read-only operation"
                    }
                ]
            }
            """;

        var result = JsonSerializer.Deserialize<GitToolInput>(json, JsonOptions);

        result.Should().NotBeNull();
        result!.Commands.Should().HaveCount(1);
        result.Commands[0].Command.Should().Be("git status");
        result.Commands[0].Risk.Should().Be("safe");
        result.Commands[0].Reason.Should().Be("Read-only operation");
    }

    [Fact]
    public void Deserialize_WithMultipleCommands_ReturnsAllCommands()
    {
        var json = """
            {
                "commands": [
                    { "command": "git add .", "risk": "safe" },
                    { "command": "git commit -m \"test\"", "risk": "moderate" },
                    { "command": "git push", "risk": "moderate" }
                ]
            }
            """;

        var result = JsonSerializer.Deserialize<GitToolInput>(json, JsonOptions);

        result.Should().NotBeNull();
        result!.Commands.Should().HaveCount(3);
    }

    [Fact]
    public void Deserialize_WithEmptyCommands_ReturnsEmptyList()
    {
        var json = """{ "commands": [] }""";

        var result = JsonSerializer.Deserialize<GitToolInput>(json, JsonOptions);

        result.Should().NotBeNull();
        result!.Commands.Should().BeEmpty();
    }

    [Fact]
    public void Deserialize_WithoutReason_ReturnsNullReason()
    {
        var json = """
            {
                "commands": [
                    { "command": "git status", "risk": "safe" }
                ]
            }
            """;

        var result = JsonSerializer.Deserialize<GitToolInput>(json, JsonOptions);

        result.Should().NotBeNull();
        result!.Commands[0].Reason.Should().BeNull();
    }
}

public class GitToolCommandTests
{
    [Fact]
    public void ToGeneratedCommand_MapsAllProperties()
    {
        var toolCommand = new GitToolCommand
        {
            Command = "git reset --hard HEAD",
            Risk = "destructive",
            Reason = "Discards uncommitted changes"
        };

        var result = toolCommand.ToGeneratedCommand();

        result.CommandText.Should().Be("git reset --hard HEAD");
        result.Risk.Should().Be("destructive");
        result.Reason.Should().Be("Discards uncommitted changes");
    }

    [Fact]
    public void ToGeneratedCommand_WithDefaultValues_MapsCorrectly()
    {
        var toolCommand = new GitToolCommand();

        var result = toolCommand.ToGeneratedCommand();

        result.CommandText.Should().BeEmpty();
        result.Risk.Should().Be("safe");
        result.Reason.Should().BeNull();
    }

    [Theory]
    [InlineData("safe")]
    [InlineData("moderate")]
    [InlineData("destructive")]
    public void ToGeneratedCommand_PreservesRiskLevel(string risk)
    {
        var toolCommand = new GitToolCommand
        {
            Command = "git status",
            Risk = risk
        };

        var result = toolCommand.ToGeneratedCommand();

        result.Risk.Should().Be(risk);
    }

    [Fact]
    public void ToGeneratedCommand_WithSpecialCharactersInCommand_PreservesThem()
    {
        var toolCommand = new GitToolCommand
        {
            Command = "git commit -m \"feat: add 'quotes' & special <chars>\"",
            Risk = "moderate"
        };

        var result = toolCommand.ToGeneratedCommand();

        result.CommandText.Should().Be("git commit -m \"feat: add 'quotes' & special <chars>\"");
    }
}
