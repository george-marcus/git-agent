using System.Reflection;
using System.Text;
using GitAgent.Models;

namespace GitAgent.Services.AI;

public interface IPromptBuilder
{
    string BuildCommandUserPrompt(string instruction, RepoContext context);
    string BuildConflictUserPrompt(ConflictSection conflict, string filePath, string fileExtension);
}

public class PromptBuilder : IPromptBuilder
{
    private readonly string _template;

    public PromptBuilder()
    {
        _template = LoadEmbeddedTemplate();
    }

    public string BuildCommandUserPrompt(string instruction, RepoContext context)
    {
        var conflictContext = BuildConflictContext(context);

        return _template
            .Replace("{{CurrentBranch}}", context.CurrentBranch)
            .Replace("{{StatusPorcelain}}", context.StatusPorcelain)
            .Replace("{{LastCommit}}", context.LastCommit)
            .Replace("{{Remotes}}", context.Remotes)
            .Replace("{{ConflictContext}}", conflictContext)
            .Replace("{{Instruction}}", instruction);
    }

    public virtual string BuildConflictUserPrompt(ConflictSection conflict, string filePath, string fileExtension)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"File: {filePath} ({fileExtension})");
        sb.AppendLine($"Conflict at lines {conflict.StartLine}-{conflict.EndLine}");
        sb.AppendLine();
        sb.AppendLine("=== OUR CHANGES (current branch) ===");
        sb.AppendLine($"Label: {conflict.OursLabel}");
        sb.AppendLine("```");
        sb.AppendLine(conflict.OursContent);
        sb.AppendLine("```");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(conflict.BaseContent))
        {
            sb.AppendLine("=== BASE (common ancestor) ===");
            sb.AppendLine("```");
            sb.AppendLine(conflict.BaseContent);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        sb.AppendLine("=== THEIR CHANGES (incoming branch) ===");
        sb.AppendLine($"Label: {conflict.TheirsLabel}");
        sb.AppendLine("```");
        sb.AppendLine(conflict.TheirsContent);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Please analyze both changes and provide a merged resolution that preserves the intent of both sides.");

        return sb.ToString();
    }
    
    private static string LoadEmbeddedTemplate()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "GitAgent.PromptTemplate.txt";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            return GetFallbackTemplate();
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string GetFallbackTemplate()
    {
        return """
            Translate the user instruction into git commands.
            Rules:
            - Only return lines that are git commands. No explanation.
            - Prefer safe operations (status, add, commit, push, branch, checkout, fetch, pull).
            - If a destructive command is required (reset --hard, clean, force push), ask for explicit confirmation by marking it as DESTRUCTIVE.

            Context:
            - Current branch: {{CurrentBranch}}
            - Staged changes (porcelain):
            {{StatusPorcelain}}
            - Last commit message:
            {{LastCommit}}

            User instruction:
            {{Instruction}}

            Output format:
            One git command per line. If multiple steps are needed, list them in the order they should run.
            """;
    }

    private static string BuildConflictContext(RepoContext context)
    {
        if (context.MergeState == MergeState.None || context.ConflictedFiles.Count == 0)
        {
            return "";
        }

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine($"- Merge state: {context.MergeState}");
        sb.AppendLine($"- Conflicted files ({context.ConflictedFiles.Count}):");

        foreach (var file in context.ConflictedFiles)
        {
            sb.AppendLine($"  - {file.FilePath} ({file.Sections.Count} conflict(s))");
            foreach (var section in file.Sections.Take(3))
            {
                sb.AppendLine($"    Lines {section.StartLine}-{section.EndLine}:");
                sb.AppendLine($"    Ours ({section.OursLabel}):");
                var oursPreview = TruncateContent(section.OursContent, 200);
                sb.AppendLine($"    ```\n    {oursPreview}\n    ```");
                sb.AppendLine($"    Theirs ({section.TheirsLabel}):");
                var theirsPreview = TruncateContent(section.TheirsContent, 200);
                sb.AppendLine($"    ```\n    {theirsPreview}\n    ```");
            }
        }

        if (!string.IsNullOrWhiteSpace(context.MergeMessage))
        {
            sb.AppendLine($"- Merge message: {TruncateContent(context.MergeMessage, 100)}");
        }

        return sb.ToString();
    }

    private static string TruncateContent(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content)) return "(empty)";
        if (content.Length <= maxLength) return content;
        return content[..maxLength] + "...";
    }
}

public class OllamaPromptBuilder : PromptBuilder
{
    public override string BuildConflictUserPrompt(ConflictSection conflict, string filePath, string fileExtension)
    {
        var baseSection = !string.IsNullOrEmpty(conflict.BaseContent)
            ? $"=== BASE (common ancestor) ===\n```\n{conflict.BaseContent}\n```"
            : "";

        return $"""
            You are an expert code merge assistant. Resolve the following git merge conflict.

            File: {filePath} ({fileExtension})
            Conflict at lines {conflict.StartLine}-{conflict.EndLine}

            === OUR CHANGES (current branch: {conflict.OursLabel}) ===
            ```
            {conflict.OursContent}
            ```

            === THEIR CHANGES (incoming branch: {conflict.TheirsLabel}) ===
            ```
            {conflict.TheirsContent}
            ```

            {baseSection}

            Instructions:
            1. Analyze both sides of the conflict
            2. Produce a merged result that preserves the intent of both changes
            3. Never include conflict markers (<<<<<<, ======, >>>>>>) in your output
            4. Ensure the resolved code is syntactically valid

            Respond with ONLY:
            1. RESOLVED_CODE: (the merged code)
            2. EXPLANATION: (brief description of how you merged the changes)
            3. CONFIDENCE: (high, medium, or low)

            Example format:
            RESOLVED_CODE:
            ```
            your merged code here
            ```
            EXPLANATION: Combined both changes by keeping X from ours and Y from theirs.
            CONFIDENCE: high
            """;
    }
}
