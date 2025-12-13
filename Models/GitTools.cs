using System.Text.Json.Serialization;

namespace GitAgent.Models;

public static class GitTools
{
    public const string GitCommandSystemPrompt = """
        You are a git command generator. Your task is to translate natural language instructions into git commands.

        Rules:
        - Use the execute_git_commands tool to return the commands
        - Prefer safe operations (status, add, commit, push, branch, checkout, fetch, pull)
        - Mark destructive commands (reset --hard, clean -fd, force push, branch -D) with risk 'destructive'
        - Mark commands that modify state (commit, push, merge) with risk 'moderate'
        - Mark read-only commands (status, log, diff, branch --list) with risk 'safe'
        - Return commands in the order they should be executed
        """;

    public const string ConflictSystemPrompt = """
        You are an expert code merge assistant. Your task is to resolve git merge conflicts intelligently.

        Rules:
        - Analyze both sides of the conflict carefully
        - Preserve the intent of both changes when possible
        - Use the resolve_conflict tool to provide your resolution
        - Consider the programming language and context when merging
        - If changes are independent, include both
        - If changes are contradictory, use your best judgment but set confidence to 'low'
        - Never include conflict markers (<<<<<<, ======, >>>>>>) in the resolved content
        - Ensure the resolved code is syntactically valid
        """;

    public const string ToolName = "execute_git_commands";

    public const string ToolDescription = """
        Execute one or more git commands. Use this tool to perform git operations based on the user's request.
        Always provide commands in the order they should be executed.
        Mark destructive commands (reset --hard, clean, force push, branch -D) with risk level 'destructive'.
        """;

    public const string ConflictToolName = "resolve_conflict";

    public const string ConflictToolDescription = """
        Resolve a git merge conflict by providing the resolved content for a conflicting section.
        Analyze both sides of the conflict and produce a merged result that preserves the intent of both changes.
        """;

    public static object GetConflictInputSchema() => new
    {
        type = "object",
        properties = new
        {
            resolved_content = new
            {
                type = "string",
                description = "The resolved content that should replace the conflict markers. This should be the final merged code without any conflict markers."
            },
            explanation = new
            {
                type = "string",
                description = "Brief explanation of how the conflict was resolved and what changes were kept from each side."
            },
            confidence = new
            {
                type = "string",
                @enum = new[] { "low", "medium", "high" },
                description = "Confidence level in the resolution: 'high' for straightforward merges, 'medium' for complex but clear merges, 'low' for ambiguous cases that need human review."
            }
        },
        required = new[] { "resolved_content", "explanation", "confidence" }
    };

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

public class ConflictToolInput
{
    [JsonPropertyName("resolved_content")]
    public string ResolvedContent { get; set; } = "";

    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = "";

    [JsonPropertyName("confidence")]
    public string Confidence { get; set; } = "low";
}
