using System.Text.Json.Serialization;

namespace GitAgent.Models;

public static class GitTools
{
    public const string ToolName = "execute_git_commands";

    public const string ToolDescription = """
        Execute one or more git commands. Use this tool to perform git operations based on the user's request.
        Always provide commands in the order they should be executed.
        Mark destructive commands (reset --hard, clean, force push, branch -D) with risk level 'destructive'.
        """;

    public static object GetInputSchema() => new
    {
        type = "object",
        properties = new
        {
            commands = new
            {
                type = "array",
                description = "List of git commands to execute in order",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        command = new
                        {
                            type = "string",
                            description = "The full git command to execute (e.g., 'git add .', 'git commit -m \"message\"')"
                        },
                        risk = new
                        {
                            type = "string",
                            @enum = new[] { "safe", "moderate", "destructive" },
                            description = "Risk level: 'safe' for read-only or reversible operations, 'moderate' for operations that modify state, 'destructive' for operations that can cause data loss"
                        },
                        reason = new
                        {
                            type = "string",
                            description = "Brief explanation of why this command is needed or why it's marked as destructive"
                        }
                    },
                    required = new[] { "command", "risk" }
                }
            }
        },
        required = new[] { "commands" }
    };
}

public class GitToolInput
{
    [JsonPropertyName("commands")]
    public List<GitToolCommand> Commands { get; set; } = [];
}

public class GitToolCommand
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "";

    [JsonPropertyName("risk")]
    public string Risk { get; set; } = "safe";

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    public GeneratedCommand ToGeneratedCommand() => new()
    {
        CommandText = Command,
        Risk = Risk,
        Reason = Reason
    };
}
