using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Resolvers;

/// <summary>
/// Submit/API/curation routing extracts Spotify episode ids from URLs and strips tracking query strings.
/// </summary>
public class SpotifyIdResolverRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When the URL path contains /episode/{id}, GetEpisodeId returns that id " +
        "because submit and URL-authority Resolve route by Spotify episode path segment.")]
    public void Episode_path_returns_episode_id()
    {
        // Arrange
        var episodeId = _fixture.CreateSpotifyId();
        var url = _fixture.DefaultSpotifyUrl(episodeId);

        // Act
        var result = SpotifyIdResolver.GetEpisodeId(url);

        // Assert
        result.Should().Be(episodeId);
    }

    [Fact(DisplayName =
        "KNOWN: When the URL has no /episode/ segment, GetEpisodeId returns empty string (not null) " +
        "because the regex Groups value is empty — callers that null-check never take that branch.")]
    public void Non_episode_path_returns_empty_string()
    {
        // Arrange
        // KNOWN: empty string vs null mismatch vs SpotifyUrlCategoriser null-check — characterize only.
        var showId = _fixture.CreateSpotifyId();
        var url = new Uri($"https://open.spotify.com/show/{showId}");

        // Act
        var result = SpotifyIdResolver.GetEpisodeId(url);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When CleanSpotifyUrl receives an episode URL without a query string, it returns the same episode URL " +
        "because submit routing may normalize idempotently.")]
    public void Clean_spotify_url_without_query_is_unchanged()
    {
        // Arrange
        var episodeId = _fixture.CreateSpotifyId();
        var expected = _fixture.DefaultSpotifyUrl(episodeId);

        // Act
        var result = expected.CleanSpotifyUrl();

        // Assert
        result.Should().Be(expected);
    }

    [Fact(DisplayName =
        "When CleanSpotifyUrl receives an episode URL with a ?si= tracking query, it returns the episode URL without query " +
        "because submit must match curated Urls.Spotify without Spotify share parameters.")]
    public void Clean_spotify_url_strips_tracking_query()
    {
        // Arrange
        var episodeId = _fixture.CreateSpotifyId();
        var expected = _fixture.DefaultSpotifyUrl(episodeId);
        var url = new Uri(expected.AbsoluteUri + "?si=11516382cd81494d");

        // Act
        var result = url.CleanSpotifyUrl();

        // Assert
        result.Should().Be(expected);
    }
}
