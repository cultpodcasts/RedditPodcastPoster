using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Merging;

/// <summary>
/// Release-date merge rules characterize current EpisodeMerger behaviour before domain extraction.
/// </summary>
public class ReleaseDateMergingRules
{
    private const string SpotifyEpisodeId = "6O1Z1s7ca0PI8Gq1rdt3j4";
    private static readonly Uri SpotifyUrl = new($"https://open.spotify.com/episode/{SpotifyEpisodeId}");

    private readonly DomainTestFixture _fixture = new();
    private readonly EpisodeMerger _merger = EpisodeDomainTestServices.CreateMerger();

    [Fact(DisplayName =
        "When stored release is midnight UTC and YouTube provides a time on the same calendar date, " +
        "merge must backfill the time from YouTube.")]
    public void YouTube_same_UTC_date_backfills_midnight_release_time()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var dateOnlyRelease = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var youTubeRelease = new DateTime(2026, 7, 1, 12, 30, 0, DateTimeKind.Utc);
        var stored = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = podcast.Id,
            Title = "Episode title",
            Release = dateOnlyRelease,
            Length = TimeSpan.FromMinutes(45),
            SpotifyId = SpotifyEpisodeId,
            Urls = new ServiceUrls { Spotify = SpotifyUrl }
        };
        var expected = EpisodeExpectation.From(stored)
            .WithRelease(youTubeRelease)
            .WithYouTube("video-id", new Uri("https://www.youtube.com/watch?v=video-id"));

        var discovered = _fixture.CreateYouTubeCatalogueEpisode(
            "video-id",
            "Episode title",
            youTubeRelease,
            TimeSpan.FromMinutes(45));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "When stored release is midnight UTC and Spotify provides a time on the same calendar date, " +
        "merge must not backfill the time — Spotify catalogue release is date-only.")]
    public void Spotify_same_date_does_not_backfill_midnight_release_time()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var dateOnlyRelease = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var spotifyRelease = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc);
        var stored = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = podcast.Id,
            Title = "Episode title",
            Release = dateOnlyRelease,
            Length = TimeSpan.FromMinutes(45),
            SpotifyId = SpotifyEpisodeId,
            Urls = new ServiceUrls { Spotify = SpotifyUrl }
        };
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(
            SpotifyEpisodeId,
            "Episode title",
            SpotifyUrl,
            spotifyRelease,
            TimeSpan.FromMinutes(45));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().BeEmpty("Spotify catalogue merge must not backfill time-of-day");
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "For YouTube-authority podcasts, re-indexing from Spotify must not replace the YouTube publish datetime " +
        "with a newer Spotify catalogue date.")]
    public void YouTube_authority_preserves_YouTube_release_on_Spotify_reindex()
    {
        // Arrange
        var podcast = _fixture.CreateCultsToConsciousnessPodcast();
        var youTubeRelease = new DateTime(2026, 6, 4, 13, 8, 6, DateTimeKind.Utc);
        var spotifyCatalogueRelease = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc);
        var youTubeUrl = new Uri("https://www.youtube.com/watch?v=UsqC0L9He2g");
        var stored = new Episode
        {
            Id = DomainTestFixture.Incidents.C2CAbuserEpisodeId,
            PodcastId = podcast.Id,
            Title = "I Confronted My Ab*ser 30 Years Later. Everything Changed",
            Release = youTubeRelease,
            Length = TimeSpan.Parse("01:28:37"),
            YouTubeId = "UsqC0L9He2g",
            SpotifyId = SpotifyEpisodeId,
            Urls = new ServiceUrls
            {
                YouTube = youTubeUrl,
                Spotify = SpotifyUrl
            }
        };
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(
            SpotifyEpisodeId,
            "I Confronted My Abuser 30 Years Later… Everything Changed",
            SpotifyUrl,
            spotifyCatalogueRelease,
            TimeSpan.Parse("01:31:59.6990000"));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty("no fields changed when Spotify catalogue date is newer than YouTube publish");
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "When stored release is midnight UTC and YouTube provides a time on a different calendar date, " +
        "merge must not backfill the time.")]
    public void YouTube_different_UTC_date_does_not_backfill_midnight_release_time()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var dateOnlyRelease = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var youTubeRelease = new DateTime(2026, 7, 2, 12, 30, 0, DateTimeKind.Utc);
        var stored = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = podcast.Id,
            Title = "Episode title",
            Release = dateOnlyRelease,
            Length = TimeSpan.FromMinutes(45),
            SpotifyId = SpotifyEpisodeId,
            Urls = new ServiceUrls { Spotify = SpotifyUrl }
        };
        var expected = EpisodeExpectation.From(stored)
            .WithYouTube("video-id", new Uri("https://www.youtube.com/watch?v=video-id"));

        var discovered = _fixture.CreateYouTubeCatalogueEpisode(
            "video-id",
            "Episode title",
            youTubeRelease,
            TimeSpan.FromMinutes(45));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Apple may upgrade a date-only stored release to a full datetime when the calendar date matches.")]
    public void Apple_same_date_backfills_midnight_release_time()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        const long appleId = 1635013492;
        var dateOnlyRelease = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var appleRelease = new DateTime(2026, 7, 1, 15, 45, 0, DateTimeKind.Utc);
        var appleUrl = new Uri($"https://podcasts.apple.com/us/podcast/episode/id{appleId}");
        var stored = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = podcast.Id,
            Title = "Episode title",
            Release = dateOnlyRelease,
            Length = TimeSpan.FromMinutes(45),
            SpotifyId = SpotifyEpisodeId,
            Urls = new ServiceUrls { Spotify = SpotifyUrl }
        };
        var expected = EpisodeExpectation.From(stored)
            .WithRelease(appleRelease)
            .WithApple(appleId, appleUrl);

        var discovered = _fixture.CreateAppleCatalogueEpisode(
            appleId,
            "Episode title",
            appleRelease,
            TimeSpan.FromMinutes(45));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }
}
