using GitAgent.Models;

namespace GitAgent.Services.AI;

public interface IResponseParser
{
    IReadOnlyList<GeneratedCommand> ParseResponse(string response);
}

public class ResponseParser : IResponseParser
{
    public IReadOnlyList<GeneratedCommand> ParseResponse(string response)
    {
        var commands = new List<GeneratedCommand>();

        if (string.IsNullOrWhiteSpace(response))
        {
            return commands;
        }

        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (trimmed.StartsWith("```"))
            {
                continue;
            }

            if (trimmed.StartsWith("#") || trimmed.StartsWith("//") || trimmed.StartsWith("*")) 
            {
                continue;
            }

            if (char.IsDigit(trimmed[0]) && trimmed.Contains('.'))
            {
                var dotIndex = trimmed.IndexOf('.');
                if (dotIndex > 0 && dotIndex < trimmed.Length - 1)
                {
                    trimmed = trimmed[(dotIndex + 1)..].Trim();
                }
            }

            if (trimmed.StartsWith("- ") || trimmed.StartsWith("• "))
            {
                trimmed = trimmed[2..].Trim();
            }

            if (!trimmed.StartsWith("git ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            commands.Add(new GeneratedCommand
            {
                CommandText = trimmed,
                Risk = "unknown" 
            });
        }

        return commands;
    }
}
