using AutoFixture;
using FluentAssertions;
using Indexer.Activities;
using Indexer.Models;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class IndexerContextMergeRules
{
    private readonly Fixture _fixture = new();

    [Fact(DisplayName =
        "The merged run fails when either pass fails.")]
    public void merged_run_fails_when_either_pass_fails()
    {
        // Arrange
        var seed = CreateSeed();
        var ok = seed with { Success = true };
        var failed = seed with { Success = false };

        // Act
        var merged = IndexerContextMerge.Merge(seed, [ok, failed]);

        // Assert
        merged.Success.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A Spotify error in either pass is a Spotify error for the hour.")]
    public void spotify_error_in_either_pass_is_spotify_error_for_the_hour()
    {
        // Arrange
        var seed = CreateSeed();
        var clean = seed with { SpotifyError = false, SkipSpotifyUrlResolving = false };
        var errored = seed with { SpotifyError = true, SkipSpotifyUrlResolving = true };

        // Act
        var merged = IndexerContextMerge.Merge(seed, [clean, errored]);

        // Assert
        merged.SpotifyError.Should().BeTrue();
        merged.SkipSpotifyUrlResolving.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A YouTube error in either pass is a YouTube error for the hour.")]
    public void youtube_error_in_either_pass_is_youtube_error_for_the_hour()
    {
        // Arrange
        var seed = CreateSeed();
        var clean = seed with { YouTubeError = false, SkipYouTubeUrlResolving = false };
        var errored = seed with { YouTubeError = true, SkipYouTubeUrlResolving = true };

        // Act
        var merged = IndexerContextMerge.Merge(seed, [clean, errored]);

        // Assert
        merged.YouTubeError.Should().BeTrue();
        merged.SkipYouTubeUrlResolving.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Duplicate-operation flags are kept per pass.")]
    public void duplicate_operation_flags_are_kept_per_pass()
    {
        // Arrange
        var seed = CreateSeed();
        var pass1Dup = seed with { DuplicateIndexerPassOperations = [true, false, false, false] };
        var pass2Dup = seed with { DuplicateIndexerPassOperations = [false, true, false, false] };

        // Act
        var merged = IndexerContextMerge.Merge(seed, [pass1Dup, pass2Dup]);

        // Assert
        merged.DuplicateIndexerPassOperations.Should().Equal(true, true, false, false);
    }

    private IndexerContext CreateSeed()
    {
        var operationIds = _fixture.CreateMany<Guid>(4).ToArray();
        var indexIds = Enumerable.Range(0, 4)
            .Select(_ => new[] { _fixture.Create<Guid>() })
            .ToArray();
        return new IndexerContext(operationIds, indexIds)
        {
            CatalogueParentIndexIds = Enumerable.Range(0, 4)
                .Select(_ => new[] { _fixture.Create<Guid>() })
                .ToArray()
        };
    }
}
