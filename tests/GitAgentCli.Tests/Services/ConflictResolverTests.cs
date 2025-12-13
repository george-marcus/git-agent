using FluentAssertions;
using GitAgent.Models;
using GitAgent.Providers;
using GitAgent.Services;
using NSubstitute;

namespace GitAgentCli.Tests.Services;

public class ConflictResolverTests
{
    private readonly ConflictResolver _resolver = new();

    [Fact]
    public async Task AnalyzeConflictsAsync_WithNoConflicts_ReturnsEmptyAnalysis()
    {
        var context = new RepoContext
        {
            MergeState = MergeState.None,
            ConflictedFiles = []
        };

        var result = await _resolver.AnalyzeConflictsAsync(context);

        result.TotalConflicts.Should().Be(0);
        result.ConflictedFileCount.Should().Be(0);
        result.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeConflictsAsync_WithConflicts_ReturnsCorrectCount()
    {
        var context = new RepoContext
        {
            MergeState = MergeState.Merging,
            ConflictedFiles =
            [
                new ConflictedFile
                {
                    FilePath = "file1.cs",
                    Sections = [new ConflictSection(), new ConflictSection()]
                },
                new ConflictedFile
                {
                    FilePath = "file2.cs",
                    Sections = [new ConflictSection()]
                }
            ]
        };

        var result = await _resolver.AnalyzeConflictsAsync(context);

        result.TotalConflicts.Should().Be(3);
        result.ConflictedFileCount.Should().Be(2);
        result.Files.Should().HaveCount(2);
    }

    [Fact]
    public async Task AnalyzeConflictsAsync_IdentifiesOursDeletedConflict()
    {
        var context = new RepoContext
        {
            MergeState = MergeState.Merging,
            ConflictedFiles =
            [
                new ConflictedFile
                {
                    FilePath = "file.cs",
                    Sections =
                    [
                        new ConflictSection
                        {
                            OursContent = "",
                            TheirsContent = "some content"
                        }
                    ]
                }
            ]
        };

        var result = await _resolver.AnalyzeConflictsAsync(context);

        result.Files[0].Sections[0].ConflictType.Should().Be(ConflictType.OursDeleted);
    }

    [Fact]
    public async Task AnalyzeConflictsAsync_IdentifiesTheirsDeletedConflict()
    {
        var context = new RepoContext
        {
            MergeState = MergeState.Merging,
            ConflictedFiles =
            [
                new ConflictedFile
                {
                    FilePath = "file.cs",
                    Sections =
                    [
                        new ConflictSection
                        {
                            OursContent = "some content",
                            TheirsContent = ""
                        }
                    ]
                }
            ]
        };

        var result = await _resolver.AnalyzeConflictsAsync(context);

        result.Files[0].Sections[0].ConflictType.Should().Be(ConflictType.TheirsDeleted);
    }

    [Fact]
    public async Task AnalyzeConflictsAsync_IdentifiesSameChangeConflict()
    {
        var context = new RepoContext
        {
            MergeState = MergeState.Merging,
            ConflictedFiles =
            [
                new ConflictedFile
                {
                    FilePath = "file.cs",
                    Sections =
                    [
                        new ConflictSection
                        {
                            OursContent = "identical content",
                            TheirsContent = "identical content"
                        }
                    ]
                }
            ]
        };

        var result = await _resolver.AnalyzeConflictsAsync(context);

        result.Files[0].Sections[0].ConflictType.Should().Be(ConflictType.SameChange);
    }

    [Fact]
    public async Task SuggestResolutionsAsync_ReturnsAcceptOursOption()
    {
        var context = new RepoContext
        {
            ConflictedFiles =
            [
                new ConflictedFile
                {
                    FilePath = "file.cs",
                    Sections =
                    [
                        new ConflictSection
                        {
                            OursContent = "our code",
                            TheirsContent = "their code"
                        }
                    ]
                }
            ]
        };

        var result = await _resolver.SuggestResolutionsAsync(context);

        result.Should().Contain(r => r.Strategy == ResolutionStrategy.AcceptOurs);
    }

    [Fact]
    public async Task SuggestResolutionsAsync_ReturnsAcceptTheirsOption()
    {
        var context = new RepoContext
        {
            ConflictedFiles =
            [
                new ConflictedFile
                {
                    FilePath = "file.cs",
                    Sections =
                    [
                        new ConflictSection
                        {
                            OursContent = "our code",
                            TheirsContent = "their code"
                        }
                    ]
                }
            ]
        };

        var result = await _resolver.SuggestResolutionsAsync(context);

        result.Should().Contain(r => r.Strategy == ResolutionStrategy.AcceptTheirs);
    }

    [Fact]
    public async Task SuggestResolutionsAsync_ReturnsCombineBothOption_WhenBothHaveContent()
    {
        var context = new RepoContext
        {
            ConflictedFiles =
            [
                new ConflictedFile
                {
                    FilePath = "file.cs",
                    Sections =
                    [
                        new ConflictSection
                        {
                            OursContent = "our code",
                            TheirsContent = "their code"
                        }
                    ]
                }
            ]
        };

        var result = await _resolver.SuggestResolutionsAsync(context);

        result.Should().Contain(r => r.Strategy == ResolutionStrategy.CombineBoth);
    }

    [Fact]
    public async Task SuggestResolutionsAsync_NoCombineOption_WhenOursEmpty()
    {
        var context = new RepoContext
        {
            ConflictedFiles =
            [
                new ConflictedFile
                {
                    FilePath = "file.cs",
                    Sections =
                    [
                        new ConflictSection
                        {
                            OursContent = "",
                            TheirsContent = "their code"
                        }
                    ]
                }
            ]
        };

        var result = await _resolver.SuggestResolutionsAsync(context);

        result.Should().NotContain(r => r.Strategy == ResolutionStrategy.CombineBoth);
    }

    [Fact]
    public async Task SuggestResolutionsAsync_WithProvider_IncludesAiSuggestion()
    {
        var provider = Substitute.For<IModelProvider>();
        provider.GenerateConflictResolution(Arg.Any<ConflictSection>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new ConflictResolutionResult
            {
                ResolvedContent = "ai merged code",
                Explanation = "Combined both changes",
                Confidence = ResolutionConfidence.High
            });

        var context = new RepoContext
        {
            ConflictedFiles =
            [
                new ConflictedFile
                {
                    FilePath = "file.cs",
                    Sections =
                    [
                        new ConflictSection
                        {
                            OursContent = "our code",
                            TheirsContent = "their code"
                        }
                    ]
                }
            ]
        };

        var result = await _resolver.SuggestResolutionsAsync(context, provider);

        result.Should().Contain(r => r.Strategy == ResolutionStrategy.AiSuggested);
        result.First(r => r.Strategy == ResolutionStrategy.AiSuggested)
            .ResolvedContent.Should().Be("ai merged code");
    }

    [Fact]
    public async Task GenerateResolutionCommandsAsync_ReturnsGitAddCommand()
    {
        var resolution = new ConflictResolution
        {
            FilePath = "src/file.cs",
            ResolvedContent = "resolved"
        };

        var result = await _resolver.GenerateResolutionCommandsAsync(resolution);

        result.Should().ContainSingle();
        result[0].CommandText.Should().Contain("git add");
        result[0].CommandText.Should().Contain("src/file.cs");
    }
}

public class ConflictResolutionResultTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var result = new ConflictResolutionResult();

        result.ResolvedContent.Should().BeEmpty();
        result.Explanation.Should().BeEmpty();
        result.Confidence.Should().Be(ResolutionConfidence.Low);
    }

    [Theory]
    [InlineData(ResolutionConfidence.Low)]
    [InlineData(ResolutionConfidence.Medium)]
    [InlineData(ResolutionConfidence.High)]
    public void Confidence_CanBeSet(ResolutionConfidence confidence)
    {
        var result = new ConflictResolutionResult { Confidence = confidence };

        result.Confidence.Should().Be(confidence);
    }
}
