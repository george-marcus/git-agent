using System.Diagnostics;
using System.Runtime.InteropServices;
using GitAgent.Models;

namespace GitAgent.Services.Execution;

public interface ICommandExecutor
{
    Task ExecuteAsync(IReadOnlyList<GeneratedCommand> commands, bool interactive);
}

public class CommandExecutor : ICommandExecutor
{
    public async Task ExecuteAsync(IReadOnlyList<GeneratedCommand> commands, bool interactive)
    {
        foreach (var cmd in commands)
        {
            var riskDisplay = cmd.Risk switch
            {
                "safe" => "\u001b[32msafe\u001b[0m",
                "destructive" => "\u001b[31mDESTRUCTIVE\u001b[0m",
                _ => "\u001b[33munknown\u001b[0m"
            };

            Console.WriteLine($"[RUN] {cmd.CommandText} (risk={riskDisplay})");

            if (!string.IsNullOrWhiteSpace(cmd.Reason))
            {
                Console.WriteLine($"      Reason: {cmd.Reason}");
            }

            if (interactive)
            {
                Console.Write("Execute? (y/N): ");
                var input = Console.ReadLine();
                if (!string.Equals(input, "y", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Skipped.");
                    continue;
                }
            }
            else if (cmd.Risk == "destructive")
            {
                Console.Write("This is a DESTRUCTIVE operation. Are you sure? (yes/N): ");
                var input = Console.ReadLine();
                if (!string.Equals(input, "yes", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Skipped.");
                    continue;
                }
            }

            var psi = CreateProcessStartInfo(cmd.CommandText);

            try
            {
                using var process = Process.Start(psi);
                if (process == null)
                {
                    Console.Error.WriteLine("Failed to start process.");
                    continue;
                }

                var stdout = await process.StandardOutput.ReadToEndAsync();
                var stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    Console.WriteLine(stdout);
                }

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    Console.Error.WriteLine(stderr);
                }

                if (process.ExitCode != 0)
                {
                    Console.Error.WriteLine($"Command exited with code {process.ExitCode}");

                    if (interactive)
                    {
                        Console.Write("Continue with remaining commands? (y/N): ");
                        var input = Console.ReadLine();
                        if (!string.Equals(input, "y", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine("Aborting remaining commands.");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error executing command: {ex.Message}");
            }
        }
    }

    private static ProcessStartInfo CreateProcessStartInfo(string command)
    {
        ProcessStartInfo psi;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            psi = new ProcessStartInfo("cmd.exe", $"/c {command}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else
        {
            psi = new ProcessStartInfo("/bin/bash", $"-c \"{command.Replace("\"", "\\\"")}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
        }

        return psi;
    }
}
