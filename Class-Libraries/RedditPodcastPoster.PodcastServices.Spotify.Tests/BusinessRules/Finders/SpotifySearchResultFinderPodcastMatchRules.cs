using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Spotify.Finders;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Finders;

/// <summary>
/// FindMatchingPodcasts is exact show-name equality after case fold; show names are trimmed, query is not.
/// </summary>
public class SpotifySearchResultFinderPodcastMatchRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly SpotifySearchResultFinder _sut = new(EpisodeDomainTestServices.CreatePlatformMatcher());

    [Fact(DisplayName =
        "When the podcasts list is null, FindMatchingPodcasts returns an empty sequence " +
        "because a missing search page must not throw during show resolution.")]
    public void Null_podcasts_returns_empty()
    {
        // Arrange
        var podcastName = _fixture.CreateTitle();

        // Act
        var result = _sut.FindMatchingPodcasts(podcastName, null);

        // Assert
        result.Should().BeEmpty();
    }

    [Theory(DisplayName =
        "When a show name matches the podcast name ignoring case (and after trimming the show name), FindMatchingPodcasts includes it " +
        "because CLI/search show resolution uses exact name equality, not fuzzy scoring.")]
    [InlineData("Cult News", "Cult News")]
    [InlineData("Cult News", "cult news")]
    [InlineData("Cult News", "  Cult News  ")]
    public void Exact_name_match_ignores_case_and_trims_show_name(string podcastName, string showName)
    {
        // Arrange
        var showId = _fixture.CreateSpotifyId();
        var shows = new List<SimpleShow>
        {
            new() { Id = showId, Name = showName },
            new() { Id = _fixture.CreateSpotifyId(), Name = _fixture.CreateTitle() }
        };

        // Act
        var result = _sut.FindMatchingPodcasts(podcastName, shows).ToList();

        // Assert
        result.Should().ContainSingle();
        result[0].Id.Should().Be(showId);
    }

    [Fact(DisplayName =
        "When no show name equals the podcast name, FindMatchingPodcasts excludes all candidates " +
        "because non-matching search hits must not become enrich targets.")]
    public void Non_matching_show_is_excluded()
    {
        // Arrange
        var shows = new List<SimpleShow>
        {
            new() { Id = _fixture.CreateSpotifyId(), Name = _fixture.CreateTitle() }
        };

        // Act
        var result = _sut.FindMatchingPodcasts(_fixture.CreateTitle(), shows);

        // Assert
        result.Should().BeEmpty();
    }
}
