using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
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
        "within five minutes of duration but outside the expanded release window and with a " +
        "disjoint title must not be selected.")]
    public void find_by_length_youtube_enrichment_does_not_duration_snipe_disjoint_title()
    {
        // Arrange — wrong-week YouTube must not claim a later Spotify row on duration alone
        var youTubeTitle = _fixture.CreateLongTitle();
        var spotifyTitle = _fixture.CreateLongTitle();
        var youTubeLength = _fixture.CreateDuration();
        var spotifyLength = youTubeLength + TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(59);
        var matchingId = _fixture.CreateSpotifyId();
        var probeRelease = DomainTestFixture.UtcAtTime(-9, new TimeSpan(3, 30, 46));
        var spotifyReleaseDate = DomainTestFixture.UtcDateDaysAgo(7);
        var episodes = new List<SimpleEpisode>
        {
            new()
            {
                Id = matchingId,
                Name = spotifyTitle,
                DurationMs = (int)spotifyLength.TotalMilliseconds,
                ReleaseDate = spotifyReleaseDate.ToString("yyyy-MM-dd")
            }
        };

        // Act
        var result = _sut.FindMatchingEpisodeByLength(
            youTubeTitle,
            youTubeLength,
            episodes,
            releaseAuthority: Service.YouTube,
            released: probeRelease,
            enrichingYouTubeDiscoveredEpisode: true);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "When enriching a YouTube-discovered episode via the Spotify finder, a sole catalogue row " +
        "with unique duration on the same calendar day is selected even when titles diverge.")]
    public void find_by_length_youtube_enrichment_accepts_unique_duration_within_release_window()
    {
        // Arrange — YouTube title vs wholly different Spotify rename; same length, same calendar day
        var youTubeTitle = _fixture.CreateLongTitle();
        var spotifyTitle = _fixture.CreateLongTitle();
        var length = _fixture.CreateDuration();
        var matchingId = _fixture.CreateSpotifyId();
        var spotifyReleaseDate = DomainTestFixture.UtcDateDaysAgo(2);
        var probeRelease = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(10));
        var episodes = new List<SimpleEpisode>
        {
            new()
            {
                Id = matchingId,
                Name = spotifyTitle,
                DurationMs = (int)length.TotalMilliseconds,
                ReleaseDate = spotifyReleaseDate.ToString("yyyy-MM-dd")
            }
        };

        // Act — probe release within 12h of Spotify midnight UTC
        var result = _sut.FindMatchingEpisodeByLength(
            youTubeTitle,
            length,
            episodes,
            releaseAuthority: Service.Spotify,
            released: probeRelease,
            enrichingYouTubeDiscoveredEpisode: true);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(matchingId);
    }

    [Fact(DisplayName =
        "When enriching a YouTube-discovered episode via the Spotify finder, a sole catalogue row with " +
        "unique duration on the same calendar day is selected even when the YouTube publish is more than " +
        "twelve hours after Spotify midnight, because Spotify catalogue releases are date-only.")]
    public void find_by_length_youtube_enrichment_accepts_same_day_spotify_outside_twelve_hours()
    {
        // Arrange
        var youTubeTitle = _fixture.CreateLongTitle();
        var spotifyTitle = _fixture.CreateLongTitle();
        var length = _fixture.CreateDuration();
        var matchingId = _fixture.CreateSpotifyId();
        var afternoonPublish = TimeSpan.FromHours(17) + TimeSpan.FromMinutes(28);
        var probeRelease = DomainTestFixture.UtcAtTime(-2, afternoonPublish);
        var episodes = new List<SimpleEpisode>
        {
            new()
            {
                Id = matchingId,
                Name = spotifyTitle,
                DurationMs = (int)length.TotalMilliseconds,
                ReleaseDate = probeRelease.ToString("yyyy-MM-dd")
            }
        };
        (probeRelease - probeRelease.Date).Should().BeGreaterThan(TimeSpan.FromHours(12));

        // Act
        var result = _sut.FindMatchingEpisodeByLength(
            youTubeTitle,
            length,
            episodes,
            releaseAuthority: Service.Spotify,
            released: probeRelease,
            enrichingYouTubeDiscoveredEpisode: true);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(matchingId);
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
        var title = _fixture.CreateLongTitle();
        var length = _fixture.CreateDuration();
        var matchingId = _fixture.CreateSpotifyId();
        var releaseDate = DomainTestFixture.UtcDateDaysAgo(5);
        var probeRelease = DomainTestFixture.UtcAtTime(-5, new TimeSpan(3, 30, 46));
        var episodes = new List<SimpleEpisode>
        {
            new()
            {
                Id = matchingId,
                Name = title,
                DurationMs = (int)length.TotalMilliseconds,
                ReleaseDate = releaseDate.ToString("yyyy-MM-dd")
            }
        };

        // Act
        var result = _sut.FindMatchingEpisodeByLength(
            title,
            length,
            episodes,
            releaseAuthority: Service.YouTube,
            released: probeRelease,
            enrichingYouTubeDiscoveredEpisode: true);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(matchingId);
    }
}