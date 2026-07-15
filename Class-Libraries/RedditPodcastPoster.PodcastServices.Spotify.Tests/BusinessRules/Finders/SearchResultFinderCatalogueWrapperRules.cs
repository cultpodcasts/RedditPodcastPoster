using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Finders;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Finders;

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
        "When enriching a YouTube-discovered episode via the Spotify finder, a sole catalogue row " +
        "within five minutes of duration but with a disjoint title must not be selected.")]
    public void find_by_length_youtube_enrichment_does_not_duration_snipe_disjoint_title()
    {
        // Arrange — wrong-week YouTube (~59:40) must not claim this week's Spotify (~62:39) on duration alone
        const string youTubeTitle =
            "Civic turnout strategies for mid-cycle ballot measures";
        const string spotifyTitle =
            "She Spent a Fortune in a Wellness Scheme with a Guest: New parenthood and a decade lost";
        var youTubeLength = TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(40);
        var spotifyLength = TimeSpan.FromMinutes(62) + TimeSpan.FromSeconds(39);
        var matchingId = _fixture.CreateSpotifyId();
        var episodes = new List<SimpleEpisode>
        {
            new()
            {
                Id = matchingId,
                Name = spotifyTitle,
                DurationMs = (int)spotifyLength.TotalMilliseconds,
                ReleaseDate = "2026-07-13"
            }
        };

        // Act
        var result = _sut.FindMatchingEpisodeByLength(
            youTubeTitle,
            youTubeLength,
            episodes,
            releaseAuthority: Service.YouTube,
            released: new DateTime(2026, 7, 11, 3, 30, 46, DateTimeKind.Utc),
            enrichingYouTubeDiscoveredEpisode: true);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "The Spotify finder never enables AcceptUniqueDurationWithoutTitleMatch: a sole catalogue row " +
        "with matching duration but a title below catalogue fuzzy thresholds is rejected.")]
    public void find_by_length_never_accepts_unique_duration_without_title_match()
    {
        // Arrange — titles chosen so FuzzySharp stays below CatalogueSameLengthMinFuzzyScore (35)
        const string probeTitle = "aaaaaaaa";
        const string catalogueTitle = "zzzzzzzz";
        var episodeLength = TimeSpan.FromMinutes(45);
        var matchingId = _fixture.CreateSpotifyId();
        var episodes = new List<SimpleEpisode>
        {
            new()
            {
                Id = matchingId,
                Name = catalogueTitle,
                DurationMs = (int)episodeLength.TotalMilliseconds,
                ReleaseDate = DomainTestFixture.UtcDateDaysAgo(2).ToString("yyyy-MM-dd")
            }
        };

        // Act
        var result = _sut.FindMatchingEpisodeByLength(
            probeTitle,
            episodeLength,
            episodes,
            enrichingYouTubeDiscoveredEpisode: false);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When enriching a YouTube-discovered episode via the Spotify finder, a catalogue row with " +
        "title confidence and duration within five minutes is still selected.")]
    public void find_by_length_youtube_enrichment_accepts_title_confident_duration_match()
    {
        // Arrange
        const string title =
            "Civic turnout strategies for mid-cycle ballot measures";
        var length = TimeSpan.FromMinutes(59) + TimeSpan.FromSeconds(40);
        var matchingId = _fixture.CreateSpotifyId();
        var episodes = new List<SimpleEpisode>
        {
            new()
            {
                Id = matchingId,
                Name = title,
                DurationMs = (int)length.TotalMilliseconds,
                ReleaseDate = "2026-07-11"
            }
        };

        // Act
        var result = _sut.FindMatchingEpisodeByLength(
            title,
            length,
            episodes,
            releaseAuthority: Service.YouTube,
            released: new DateTime(2026, 7, 11, 3, 30, 46, DateTimeKind.Utc),
            enrichingYouTubeDiscoveredEpisode: true);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(matchingId);
    }
}