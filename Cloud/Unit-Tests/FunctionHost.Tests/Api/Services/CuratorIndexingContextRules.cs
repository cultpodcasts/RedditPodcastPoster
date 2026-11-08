using Api.Services;
using FluentAssertions;
using RedditPodcastPoster.Configuration.Options;
using Xunit;

namespace FunctionHost.Tests.Api.Services;

public class CuratorIndexingContextRules
{
    [Fact(DisplayName =
        "Default curator index keeps ReleasedSince from indexer ReleasedDaysAgo so hub Index stays a short lookback.")]
    public void default_curator_index_keeps_released_since_lookback()
    {
        // Arrange
        var options = new IndexerOptions { ReleasedDaysAgo = 2 };

        // Act
        var context = CuratorIndexingContext.Create(options);

        // Assert
        context.ReleasedSince.Should().NotBeNull();
        context.IndexSpotify.Should().BeTrue();
        context.SkipPodcastDiscovery.Should().BeTrue();
        context.UseYouTubeIndexingHints.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Backlog curator index clears ReleasedSince so provider pagination walks past the two-day hub Index floor.")]
    public void backlog_curator_index_clears_released_since()
    {
        // Arrange
        var options = new IndexerOptions { ReleasedDaysAgo = 2 };

        // Act
        var context = CuratorIndexingContext.Create(options, backlog: true);

        // Assert
        context.ReleasedSince.Should().BeNull();
        context.IndexSpotify.Should().BeTrue();
        context.SkipExpensiveYouTubeQueries.Should().BeFalse();
        context.SkipExpensiveSpotifyQueries.Should().BeFalse();
        context.SkipPodcastDiscovery.Should().BeTrue();
        context.UseYouTubeIndexingHints.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Default curator index still requires ReleasedDaysAgo; backlog mode does not, because ReleasedSince is unset on purpose.")]
    public void default_requires_released_days_ago_but_backlog_does_not()
    {
        // Arrange
        var options = new IndexerOptions { ReleasedDaysAgo = null };

        // Act
        var actDefault = () => CuratorIndexingContext.Create(options);
        var actBacklog = () => CuratorIndexingContext.Create(options, backlog: true);

        // Assert
        actDefault.Should().Throw<InvalidOperationException>();
        actBacklog().ReleasedSince.Should().BeNull();
    }
}
