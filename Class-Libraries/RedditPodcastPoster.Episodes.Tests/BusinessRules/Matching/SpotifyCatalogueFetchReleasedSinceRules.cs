using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// Spotify catalogue fetch window must include date-only rows the matcher can still accept.
/// </summary>
public class SpotifyCatalogueFetchReleasedSinceRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "GetSpotifyCatalogueFetchReleasedSince widens the indexing ReleasedSince floor by the " +
        "audio-release consideration threshold so date-only Spotify catalogue rows that still match " +
        "within GetToleranceTicks are not dropped by PaginateEpisodes before enrichment.")]
    public void spotify_catalogue_fetch_released_since_widens_by_consideration_threshold()
    {
        // Arrange
        var indexingReleasedSince = DomainTestFixture.UtcDateDaysAgo(2);

        // Act
        var fetchReleasedSince = EpisodeReleaseTolerance.GetSpotifyCatalogueFetchReleasedSince(indexingReleasedSince);
        var nullFetch = EpisodeReleaseTolerance.GetSpotifyCatalogueFetchReleasedSince(null);

        // Assert
        fetchReleasedSince.Should().Be(
            indexingReleasedSince.Date.Subtract(
                EpisodeReleaseTolerance.YouTubeAuthorityToAudioReleaseConsiderationThreshold));
        nullFetch.Should().BeNull();
    }
}
