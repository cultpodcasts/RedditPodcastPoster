using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Spotify.Finders;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.Finders;

/// <summary>
/// Thin-wrapper rules: Spotify finder delegates catalogue matching to the domain matcher.
/// </summary>
public class SearchResultFinderCatalogueWrapperRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly SpotifySearchResultFinder _sut = new(EpisodeDomainTestServices.CreatePlatformMatcher());

    [Fact(DisplayName =
        "When the Spotify finder resolves by release date, " +
        "it returns the SimpleEpisode whose title and calendar date match the probe.")]
    public void find_by_date_delegates_to_domain_matcher_and_maps_back()
    {
        // Arrange
        var sharedTitle = _fixture.CreateTitle();
        var release = DomainTestFixture.UtcDateDaysAgo(7);
        var spotifyId = _fixture.CreateSpotifyId();
        var episodes = new List<SimpleEpisode>
        {
            new()
            {
                Id = spotifyId,
                Name = sharedTitle,
                DurationMs = (int)_fixture.CreateDuration().TotalMilliseconds,
                ReleaseDate = release.ToString("yyyy-MM-dd")
            }
        };

        // Act
        var result = _sut.FindMatchingEpisodeByDate(sharedTitle, release, episodes);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(spotifyId);
    }

    [Fact(DisplayName =
        "When the Spotify finder applies a reducer callback, " +
        "excluded SimpleEpisodes are not returned even when they would otherwise match.")]
    public void find_by_length_passes_reducer_through_to_domain_matcher()
    {
        // Arrange
        var sharedTitle = _fixture.CreateTitle();
        var sharedLength = _fixture.CreateDuration();
        var assignedId = _fixture.CreateSpotifyId();
        var availableId = _fixture.CreateSpotifyId();
        var episodes = new List<SimpleEpisode>
        {
            new()
            {
                Id = assignedId,
                Name = sharedTitle,
                DurationMs = (int)sharedLength.TotalMilliseconds,
                ReleaseDate = DomainTestFixture.UtcDateDaysAgo(2).ToString("yyyy-MM-dd")
            },
            new()
            {
                Id = availableId,
                Name = sharedTitle,
                DurationMs = (int)sharedLength.TotalMilliseconds,
                ReleaseDate = DomainTestFixture.UtcDateDaysAgo(2).ToString("yyyy-MM-dd")
            }
        };
        var assignedIds = new HashSet<string> { assignedId };

        // Act
        var result = _sut.FindMatchingEpisodeByLength(
            sharedTitle,
            sharedLength,
            episodes,
            reducer: e => !assignedIds.Contains(e.Id));

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(availableId);
    }

    [Fact(DisplayName =
        "When the Spotify finder accepts a unique-duration match without title overlap, " +
        "it maps the matched SimpleEpisode back to the platform API type.")]
    public void find_by_length_maps_unique_duration_match_back_to_simple_episode()
    {
        // Arrange
        var episodeLength = _fixture.CreateDuration();
        var otherLength = episodeLength + TimeSpan.FromMinutes(30);
        var matchingId = _fixture.CreateSpotifyId();
        var episodes = new List<SimpleEpisode>
        {
            new()
            {
                Id = matchingId,
                Name = _fixture.CreateTitle(),
                DurationMs = (int)episodeLength.TotalMilliseconds,
                ReleaseDate = DomainTestFixture.UtcDateDaysAgo(2).ToString("yyyy-MM-dd")
            },
            new()
            {
                Id = _fixture.CreateSpotifyId(),
                Name = _fixture.CreateTitle(),
                DurationMs = (int)otherLength.TotalMilliseconds,
                ReleaseDate = DomainTestFixture.UtcDateDaysAgo(2).ToString("yyyy-MM-dd")
            }
        };

        // Act
        var result = _sut.FindMatchingEpisodeByLength(
            _fixture.CreateTitle(),
            episodeLength,
            episodes,
            acceptUniqueDurationWithoutTitleMatch: true);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(matchingId);
    }
}
