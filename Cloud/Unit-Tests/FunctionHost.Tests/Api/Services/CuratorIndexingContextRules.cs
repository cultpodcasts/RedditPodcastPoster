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
        "CreateBacklog sets ReleasedSince to UnixEpoch so PodcastUpdater accepts the context while the walk is still past the two-day hub Index floor.")]
    public void backlog_curator_index_uses_unix_epoch_released_since()
    {
        // Arrange
        // CreateBacklog needs no IndexerOptions; ReleasedSince is the far-past backlog floor.

        // Act
        var context = CuratorIndexingContext.CreateBacklog();

        // Assert
        context.ReleasedSince.Should().Be(CuratorIndexingContext.BacklogReleasedSince);
        context.ReleasedSince.Should().Be(DateTime.UnixEpoch);
        context.IndexSpotify.Should().BeTrue();
        context.SkipExpensiveYouTubeQueries.Should().BeFalse();
        context.SkipExpensiveSpotifyQueries.Should().BeFalse();
        context.SkipPodcastDiscovery.Should().BeTrue();
        context.UseYouTubeIndexingHints.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Create still requires ReleasedDaysAgo; CreateBacklog does not, because it uses the fixed backlog ReleasedSince floor.")]
    public void default_requires_released_days_ago_but_backlog_does_not()
    {
        // Arrange
        var options = new IndexerOptions { ReleasedDaysAgo = null };

        // Act
        var actDefault = () => CuratorIndexingContext.Create(options);
        var actBacklog = () => CuratorIndexingContext.CreateBacklog();

        // Assert
        actDefault.Should().Throw<InvalidOperationException>();
        actBacklog().ReleasedSince.Should().Be(CuratorIndexingContext.BacklogReleasedSince);
    }
}
