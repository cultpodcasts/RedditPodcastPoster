using FluentAssertions;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// Characterizes <see cref="EpisodeIdentityExtensions"/> identity and Spotify ID resolution helpers.
/// </summary>
public class EpisodeIdentityExtensionsRules
{
    private readonly DomainTestFixture _fixture = new();

    public static TheoryData<string> SpotifyIdentityScenarios =>
        new()
        {
            "id_present",
            "url_only",
            "neither"
        };

    [Theory(DisplayName =
        "HasSpotifyIdentity is true when a Spotify id or Spotify URL is present, otherwise false.")]
    [MemberData(nameof(SpotifyIdentityScenarios))]
    public void has_spotify_identity_scenarios(string scenario)
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = scenario switch
        {
            "id_present" => _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast),
            "url_only" => _fixture.CreateStoredEpisode(podcast, e =>
            {
                e.SpotifyId = string.Empty;
                e.Urls = new ServiceUrls
                {
                    Spotify = new Uri("https://open.spotify.com/episode/abc123DEF456")
                };
            }),
            "neither" => _fixture.CreateStoredEpisode(podcast, e =>
            {
                e.SpotifyId = string.Empty;
                e.Urls = new ServiceUrls();
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
        };

        // Act
        var hasIdentity = episode.HasSpotifyIdentity();

        // Assert
        hasIdentity.Should().Be(scenario is "id_present" or "url_only");
    }

    public static TheoryData<string> AppleIdentityScenarios =>
        new()
        {
            "id_present",
            "url_only",
            "neither"
        };

    [Theory(DisplayName =
        "HasAppleIdentity is true when a positive Apple id or Apple URL is present, otherwise false.")]
    [MemberData(nameof(AppleIdentityScenarios))]
    public void has_apple_identity_scenarios(string scenario)
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = scenario switch
        {
            "id_present" => _fixture.CreateStoredEpisode(podcast, e =>
            {
                e.AppleId = _fixture.CreateAppleId();
                e.Urls = new ServiceUrls();
            }),
            "url_only" => _fixture.CreateStoredEpisode(podcast, e =>
            {
                e.AppleId = null;
                e.Urls = new ServiceUrls
                {
                    Apple = new Uri("https://podcasts.apple.com/us/podcast/episode/id1234567890")
                };
            }),
            "neither" => _fixture.CreateStoredEpisode(podcast, e =>
            {
                e.AppleId = null;
                e.Urls = new ServiceUrls();
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null)
        };

        // Act
        var hasIdentity = episode.HasAppleIdentity();

        // Assert
        hasIdentity.Should().Be(scenario is "id_present" or "url_only");
    }

    [Fact(DisplayName =
        "ResolveSpotifyEpisodeId returns the explicit Spotify id when present, " +
        "preferring it over extracting from the Spotify URL.")]
    public void resolve_spotify_episode_id_prefers_explicit_id()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var url = new Uri("https://open.spotify.com/episode/fromUrlOnly123");

        // Act
        var resolved = EpisodeIdentityExtensions.ResolveSpotifyEpisodeId(spotifyId, url);

        // Assert
        resolved.Should().Be(spotifyId);
    }

    [Fact(DisplayName =
        "ResolveSpotifyEpisodeId extracts the episode id from a Spotify episode URL " +
        "when no explicit Spotify id is stored.")]
    public void resolve_spotify_episode_id_from_url_when_id_missing()
    {
        // Arrange
        const string episodeId = "fromUrlOnly123";
        var url = new Uri($"https://open.spotify.com/episode/{episodeId}");

        // Act
        var resolved = EpisodeIdentityExtensions.ResolveSpotifyEpisodeId(string.Empty, url);

        // Assert
        resolved.Should().Be(episodeId);
    }

    [Fact(DisplayName =
        "ResolveSpotifyEpisodeId returns null when both Spotify id and URL are absent.")]
    public void resolve_spotify_episode_id_null_when_id_and_url_missing()
    {
        // Act
        var resolved = EpisodeIdentityExtensions.ResolveSpotifyEpisodeId(string.Empty, null);

        // Assert
        resolved.Should().BeNull();
    }

    [Fact(DisplayName =
        "SpotifyEpisodesMatch is true when both episodes resolve to the same Spotify episode id, " +
        "including URL-only identity on one side.")]
    public void spotify_episodes_match_when_resolved_ids_equal()
    {
        // Arrange
        const string episodeId = "sharedEpisodeId99";
        var left = _fixture.CreateEpisode(e =>
        {
            e.SpotifyId = episodeId;
            e.Urls = new ServiceUrls();
        });
        var right = _fixture.CreateEpisode(e =>
        {
            e.SpotifyId = string.Empty;
            e.Urls = new ServiceUrls
            {
                Spotify = new Uri($"https://open.spotify.com/episode/{episodeId}")
            };
        });

        // Act
        var matches = EpisodeIdentityExtensions.SpotifyEpisodesMatch(left, right);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "IncomingPlatformIdOwnedByAnotherEpisode is true when another stored episode already owns " +
        "the incoming Spotify episode id.")]
    public void incoming_spotify_id_owned_by_another_episode()
    {
        // Arrange
        var sharedSpotifyId = _fixture.CreateSpotifyId();
        var candidate = _fixture.CreateEpisode();
        var other = _fixture.CreateEpisode(e => e.SpotifyId = sharedSpotifyId);
        var incoming = _fixture.CreateEpisode(e => e.SpotifyId = sharedSpotifyId);

        // Act
        var owned = EpisodeIdentityExtensions.IncomingPlatformIdOwnedByAnotherEpisode(
            candidate,
            incoming,
            [candidate, other]);

        // Assert
        owned.Should().BeTrue();
    }

    [Fact(DisplayName =
        "IncomingPlatformIdOwnedByAnotherEpisode is true when another stored episode already owns " +
        "the incoming Apple episode id.")]
    public void incoming_apple_id_owned_by_another_episode()
    {
        // Arrange
        var sharedAppleId = _fixture.CreateAppleId();
        var candidate = _fixture.CreateEpisode();
        var other = _fixture.CreateEpisode(e => e.AppleId = sharedAppleId);
        var incoming = _fixture.CreateEpisode(e => e.AppleId = sharedAppleId);

        // Act
        var owned = EpisodeIdentityExtensions.IncomingPlatformIdOwnedByAnotherEpisode(
            candidate,
            incoming,
            [candidate, other]);

        // Assert
        owned.Should().BeTrue();
    }

    [Fact(DisplayName =
        "IncomingPlatformIdOwnedByAnotherEpisode is false when only the candidate itself carries " +
        "the same platform id as the incoming episode.")]
    public void incoming_platform_id_not_owned_when_only_candidate_matches()
    {
        // Arrange
        var sharedSpotifyId = _fixture.CreateSpotifyId();
        var candidate = _fixture.CreateEpisode(e => e.SpotifyId = sharedSpotifyId);
        var incoming = _fixture.CreateEpisode(e => e.SpotifyId = sharedSpotifyId);

        // Act
        var owned = EpisodeIdentityExtensions.IncomingPlatformIdOwnedByAnotherEpisode(
            candidate,
            incoming,
            [candidate]);

        // Assert
        owned.Should().BeFalse();
    }
}
