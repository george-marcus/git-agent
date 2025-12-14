using GitAgent.Providers;
using GitAgent.Services;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.Hosting;


namespace GitAgent.Commands
{
    internal class ConflictsCommand
    {
        public static Command BuildConflictsCommand()
        {
            var conflictsCmd = new Command("conflicts", "Analyze and resolve merge conflicts with AI assistance");

            var suggestOption = new Option<bool>(["--suggest", "-s"], () => false, "Show AI-suggested resolutions");
            var resolveOption = new Option<bool>(["--resolve", "-r"], () => false, "Interactively resolve conflicts");
            var applyOption = new Option<bool>(["--apply", "-a"], () => false, "Auto-apply AI-suggested resolutions");
            var providerOption = new Option<string?>(["--provider", "-p"], "Override the active provider for AI suggestions");
            var fileArg = new Argument<string?>("file", () => null, "Specific file to analyze (optional)");

            conflictsCmd.AddOption(suggestOption);
            conflictsCmd.AddOption(resolveOption);
            conflictsCmd.AddOption(applyOption);
            conflictsCmd.AddOption(providerOption);
            conflictsCmd.AddArgument(fileArg);

            conflictsCmd.SetHandler(async context =>
            {
                var suggest = context.ParseResult.GetValueForOption(suggestOption);
                var resolve = context.ParseResult.GetValueForOption(resolveOption);
                var apply = context.ParseResult.GetValueForOption(applyOption);
                var providerOverride = context.ParseResult.GetValueForOption(providerOption);
                var file = context.ParseResult.GetValueForArgument(fileArg);

                var host = context.GetHost();
                var gitInspector = host.Services.GetRequiredService<IGitInspector>();
                var conflictResolver = host.Services.GetRequiredService<IConflictResolver>();
                var providerFactory = host.Services.GetRequiredService<IProviderFactory>();

                var ctx = await gitInspector.BuildRepoContextAsync();

                if (ctx.MergeState == Models.MergeState.None)
                {
                    await Console.Out.WriteLineAsync("No merge in progress.");
                    return;
                }

                if (ctx.ConflictedFiles.Count == 0)
                {
                    await Console.Out.WriteLineAsync($"Merge state: {ctx.MergeState}");
                    await Console.Out.WriteLineAsync("No conflicts detected. You can continue with:");
                    await Console.Out.WriteLineAsync($"  git {GetContinueCommand(ctx.MergeState)}");
                    return;
                }

                var filesToAnalyze = ctx.ConflictedFiles;
                if (!string.IsNullOrEmpty(file))
                {
                    filesToAnalyze = ctx.ConflictedFiles.Where(f => f.FilePath.Contains(file)).ToList();
                    if (filesToAnalyze.Count == 0)
                    {
                        await Console.Error.WriteLineAsync($"No conflicts found in files matching: {file}");
                        return;
                    }
                }

                var analysis = await conflictResolver.AnalyzeConflictsAsync(ctx);

                await Console.Out.WriteLineAsync($"\u001b[33mMerge State:\u001b[0m {ctx.MergeState}");
                await Console.Out.WriteLineAsync($"\u001b[33mTotal Conflicts:\u001b[0m {analysis.TotalConflicts} in {analysis.ConflictedFileCount} file(s)");
                await Console.Out.WriteLineAsync();

                foreach (var fileAnalysis in analysis.Files)
                {
                    await Console.Out.WriteLineAsync($"\u001b[36m{fileAnalysis.FilePath}\u001b[0m ({fileAnalysis.ConflictCount} conflict(s))");

                    foreach (var section in fileAnalysis.Sections)
                    {
                        var typeColor = section.ConflictType switch
                        {
                            Services.ConflictType.SameChange => "\u001b[32m",
                            Services.ConflictType.AdjacentChanges => "\u001b[33m",
                            _ => "\u001b[31m"
                        };
                        await Console.Out.WriteLineAsync($"  Lines {section.StartLine}-{section.EndLine}: {typeColor}{section.ConflictType}\u001b[0m");
                        await Console.Out.WriteLineAsync($"    {section.Description}");
                        await Console.Out.WriteLineAsync($"    Ours: {section.OursLabel} | Theirs: {section.TheirsLabel}");
                    }
                    await Console.Out.WriteLineAsync();
                }

                if (suggest || apply)
                {
                    // Get provider for AI suggestions
                    Providers.IModelProvider? provider = null;
                    try
                    {
                        provider = string.IsNullOrWhiteSpace(providerOverride)
                            ? await providerFactory.CreateProviderAsync()
                            : await providerFactory.CreateProviderAsync(providerOverride);

                        if (suggest)
                        {
                            await Console.Out.WriteLineAsync($"Using provider: {providerOverride ?? "default"}");
                        }
                    }
                    catch (Exception ex)
                    {
                        await Console.Error.WriteLineAsync($"Warning: Could not initialize AI provider: {ex.Message}");
                        await Console.Error.WriteLineAsync("Falling back to heuristic suggestions.");
                    }

                    await Console.Out.WriteLineAsync("\u001b[33mGenerating AI Resolutions...\u001b[0m");
                    var resolutions = await conflictResolver.SuggestResolutionsAsync(ctx, provider);

                    if (suggest)
                    {
                        await Console.Out.WriteLineAsync(new string('-', 40));
                        var grouped = resolutions.GroupBy(r => r.FilePath);

                        foreach (var group in grouped)
                        {
                            await Console.Out.WriteLineAsync($"\u001b[36m{group.Key}\u001b[0m");
                            var sectionGroups = group.GroupBy(r => (r.Section.StartLine, r.Section.EndLine));

                            foreach (var sectionGroup in sectionGroups)
                            {
                                await Console.Out.WriteLineAsync($"  Lines {sectionGroup.Key.StartLine}-{sectionGroup.Key.EndLine}:");
                                int optionNum = 1;
                                foreach (var resolution in sectionGroup)
                                {
                                    var strategyColor = resolution.Strategy switch
                                    {
                                        Services.ResolutionStrategy.AiSuggested => "\u001b[35m",
                                        Services.ResolutionStrategy.AcceptOurs => "\u001b[32m",
                                        Services.ResolutionStrategy.AcceptTheirs => "\u001b[34m",
                                        _ => "\u001b[33m"
                                    };
                                    await Console.Out.WriteLineAsync($"    {optionNum}. {strategyColor}[{resolution.Strategy}]\u001b[0m {resolution.Description}");
                                    optionNum++;
                                }
                            }
                            await Console.Out.WriteLineAsync();
                        }
                    }

                    if (apply)
                    {
                        await Console.Out.WriteLineAsync();
                        await Console.Out.WriteLineAsync("\u001b[33mApplying AI Resolutions...\u001b[0m");
                        await Console.Out.WriteLineAsync(new string('-', 40));

                        // Get only AI-suggested resolutions
                        var aiResolutions = resolutions
                            .Where(r => r.Strategy == Services.ResolutionStrategy.AiSuggested)
                            .ToList();

                        if (aiResolutions.Count == 0)
                        {
                            await Console.Out.WriteLineAsync("No AI resolutions available to apply.");
                        }
                        else
                        {
                            var groupedByFile = aiResolutions.GroupBy(r => r.FilePath);
                            int appliedCount = 0;
                            int failedCount = 0;

                            foreach (var fileGroup in groupedByFile)
                            {
                                await Console.Out.WriteAsync($"  {fileGroup.Key}: ");
                                var fileResolutions = fileGroup.ToList();

                                var success = await conflictResolver.ApplyAllResolutionsAsync(fileResolutions, fileGroup.Key);
                                if (success)
                                {
                                    await Console.Out.WriteLineAsync($"\u001b[32m{fileResolutions.Count} conflict(s) resolved\u001b[0m");
                                    appliedCount += fileResolutions.Count;
                                }
                                else
                                {
                                    await Console.Out.WriteLineAsync("\u001b[31mFailed to apply\u001b[0m");
                                    failedCount += fileResolutions.Count;
                                }
                            }

                            await Console.Out.WriteLineAsync();
                            await Console.Out.WriteLineAsync($"Applied: {appliedCount}, Failed: {failedCount}");

                            if (appliedCount > 0)
                            {
                                await Console.Out.WriteLineAsync();
                                await Console.Out.WriteLineAsync("Next steps:");
                                await Console.Out.WriteLineAsync("  1. Review the resolved files");
                                await Console.Out.WriteLineAsync("  2. git add <resolved-files>");
                                await Console.Out.WriteLineAsync($"  3. git {GetContinueCommand(ctx.MergeState)}");
                            }
                        }
                    }
                }

                if (resolve)
                {
                    await Console.Out.WriteLineAsync("\u001b[33mInteractive Resolution Mode\u001b[0m");
                    await Console.Out.WriteLineAsync("For each conflict, choose a resolution strategy:");
                    await Console.Out.WriteLineAsync();

                    var resolvedFiles = new List<string>();

                    foreach (var conflictFile in filesToAnalyze)
                    {
                        await Console.Out.WriteLineAsync($"\u001b[36m{conflictFile.FilePath}\u001b[0m");
                        var fileResolved = false;

                        foreach (var section in conflictFile.Sections)
                        {
                            await Console.Out.WriteLineAsync($"\n  Conflict at lines {section.StartLine}-{section.EndLine}:");
                            await Console.Out.WriteLineAsync($"  \u001b[32mOurs ({section.OursLabel}):\u001b[0m");
                            PrintIndented(section.OursContent, "    ");
                            await Console.Out.WriteLineAsync($"  \u001b[34mTheirs ({section.TheirsLabel}):\u001b[0m");
                            PrintIndented(section.TheirsContent, "    ");

                            await Console.Out.WriteLineAsync();
                            await Console.Out.WriteLineAsync("  Options:");
                            await Console.Out.WriteLineAsync("    1. Accept ours");
                            await Console.Out.WriteLineAsync("    2. Accept theirs");
                            await Console.Out.WriteLineAsync("    3. Combine (ours + theirs)");
                            await Console.Out.WriteLineAsync("    4. Skip this conflict");

                            await Console.Out.WriteAsync("  Choose [1-4]: ");
                            var input = Console.ReadLine()?.Trim();

                            string? resolvedContent = input switch
                            {
                                "1" => section.OursContent,
                                "2" => section.TheirsContent,
                                "3" => section.OursContent + "\n" + section.TheirsContent,
                                _ => null
                            };

                            if (resolvedContent != null)
                            {
                                var resolution = new Services.ConflictResolution
                                {
                                    FilePath = conflictFile.FilePath,
                                    Section = section,
                                    Strategy = Services.ResolutionStrategy.Manual,
                                    Description = "Manual resolution",
                                    ResolvedContent = resolvedContent
                                };

                                var success = await conflictResolver.ApplyResolutionAsync(resolution);
                                if (success)
                                {
                                    await Console.Out.WriteLineAsync($"  \u001b[32mResolution applied.\u001b[0m");
                                    fileResolved = true;
                                }
                                else
                                {
                                    await Console.Out.WriteLineAsync($"  \u001b[31mFailed to apply resolution.\u001b[0m");
                                }
                            }
                            else
                            {
                                await Console.Out.WriteLineAsync($"  \u001b[33mSkipped.\u001b[0m");
                            }
                        }

                        if (fileResolved)
                        {
                            resolvedFiles.Add(conflictFile.FilePath);
                        }
                    }

                    await Console.Out.WriteLineAsync();
                    if (resolvedFiles.Count > 0)
                    {
                        await Console.Out.WriteLineAsync("Resolved files:");
                        foreach (var resolvedFile in resolvedFiles)
                        {
                            await Console.Out.WriteLineAsync($"  {resolvedFile}");
                        }
                        await Console.Out.WriteLineAsync();
                        await Console.Out.WriteLineAsync("Next steps:");
                        await Console.Out.WriteLineAsync($"  git add {string.Join(" ", resolvedFiles.Select(f => $"\"{f}\""))}");
                        await Console.Out.WriteLineAsync($"  git {GetContinueCommand(ctx.MergeState)}");
                    }
                    else
                    {
                        await Console.Out.WriteLineAsync("No conflicts were resolved.");
                    }
                }
                else if (!suggest && !apply)
                {
                    await Console.Out.WriteLineAsync("Use --suggest (-s) to see AI-suggested resolutions.");
                    await Console.Out.WriteLineAsync("Use --apply (-a) to auto-apply AI resolutions.");
                    await Console.Out.WriteLineAsync("Use --resolve (-r) to interactively resolve conflicts.");
                }
            });

            return conflictsCmd;
        }

        private static string GetContinueCommand(Models.MergeState state) => state switch
        {
            Models.MergeState.Merging => "merge --continue",
            Models.MergeState.Rebasing => "rebase --continue",
            Models.MergeState.CherryPicking => "cherry-pick --continue",
            Models.MergeState.Reverting => "revert --continue",
            _ => "commit"
        };

        private static void PrintIndented(string content, string indent)
        {
            if (string.IsNullOrEmpty(content))
            {
                Console.WriteLine($"{indent}(empty)");
                return;
            }
            foreach (var line in content.Split('\n').Take(5))
            {
                Console.WriteLine($"{indent}{line}");
            }
            var lineCount = content.Split('\n').Length;
            if (lineCount > 5)
            {
                Console.WriteLine($"{indent}... ({lineCount - 5} more lines)");
            }
        }
    }
}