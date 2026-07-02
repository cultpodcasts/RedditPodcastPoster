using FluentAssertions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.PodcastServices.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace RedditPodcastPoster.Persistence.Tests;

public class C2CAbuserEpisodeMergeTests
{
    private const long C2CDelayTicks = -27216000000000;
    private static readonly Guid ExistingId = Guid.Parse("7dd136da-84ae-4c02-81be-9baa5f4c3362");
    private const string SpotifyId = "6O1Z1s7ca0PI8Gq1rdt3j4";

    private static Podcast CreatePodcast() => new()
    {
        Id = Guid.Parse("1aa72d3d-f1e4-458f-a172-62990ef6c200"),
        Name = "Cults to Consciousness",
        ReleaseAuthority = Service.YouTube,
        YouTubePublicationOffset = C2CDelayTicks,
        SpotifyId = "6oTbi9wKZ2czCvSwBKxxoH"
    };

    [Theory]
    [InlineData("2026-07-06T00:00:00Z")]
    [InlineData("2026-07-05T00:00:00Z")]
    [InlineData("2026-07-07T00:00:00Z")]
    [InlineData("2026-07-02T00:00:00Z")]
    public void EpisodesReleaseMatch_WhenSpotifyReleaseVaries(string spotifyReleaseText)
    {
        var podcast = CreatePodcast();
        var youTubeRelease = new DateTime(2026, 6, 4, 13, 8, 6, DateTimeKind.Utc);
        var spotifyRelease = DateTime.Parse(spotifyReleaseText, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var length = TimeSpan.Parse("01:28:37");

        var existing = new Episode
        {
            Id = ExistingId,
            Title = "I Confronted My Ab*ser 30 Years Later. Everything Changed",
            Release = youTubeRelease,
            Length = length,
            YouTubeId = "UsqC0L9He2g",
            Urls = new ServiceUrls { YouTube = new Uri("https://www.youtube.com/watch?v=UsqC0L9He2g") }
        };
        var incoming = Episode.FromSpotify(
            SpotifyId,
            "I Confronted My Abuser 30 Years Later… Everything Changed",
            "description",
            length,
            false,
            spotifyRelease,
            new Uri($"https://open.spotify.com/episode/{SpotifyId}"),
            null);

        var releaseMatches = EpisodeReleaseMatchTolerance.EpisodesReleaseMatch(podcast, existing, incoming);
        var matcher = new EpisodeMatcher(NullLogger<EpisodeMatcher>.Instance);
        var isMatch = matcher.IsMatch(existing, incoming, episodeMatchRegex: null, podcast);

        // Diagnostic output for investigation
        releaseMatches.Should().BeTrue($"Spotify release {spotifyRelease:O} should align after delay adjustment");
        isMatch.Should().BeTrue();
    }

    [Fact]
    public void MergeEpisodes_WhenSpotifyIncomingMatchesYouTubeOnlyEpisode_MergesOntoExisting()
    {
        var podcast = CreatePodcast();
        var youTubeRelease = new DateTime(2026, 6, 4, 13, 8, 6, DateTimeKind.Utc);
        var spotifyRelease = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc);
        var youTubeLength = TimeSpan.Parse("01:28:37");
        var spotifyLength = TimeSpan.Parse("01:31:59.6990000");

        var existing = new Episode
        {
            Id = ExistingId,
            PodcastId = podcast.Id,
            Title = "I Confronted My Ab*ser 30 Years Later. Everything Changed",
            Release = youTubeRelease,
            Length = youTubeLength,
            YouTubeId = "UsqC0L9He2g",
            Urls = new ServiceUrls { YouTube = new Uri("https://www.youtube.com/watch?v=UsqC0L9He2g") }
        };
        var incoming = Episode.FromSpotify(
            SpotifyId,
            "I Confronted My Abuser 30 Years Later… Everything Changed",
            "description",
            spotifyLength,
            false,
            spotifyRelease,
            new Uri($"https://open.spotify.com/episode/{SpotifyId}"),
            null);

        var sut = new EpisodeMerger(new EpisodeMatcher(NullLogger<EpisodeMatcher>.Instance));
        var result = sut.MergeEpisodes(podcast, [existing], [incoming]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(ExistingId);
        existing.SpotifyId.Should().Be(SpotifyId);
        existing.Release.Should().Be(youTubeRelease);
    }

    [Fact]
    public void MergeEpisodes_WhenSpotifyReindexMatchesExistingSpotifyId_PreservesYouTubeReleaseDate()
    {
        var podcast = CreatePodcast();
        var youTubeRelease = new DateTime(2026, 6, 4, 13, 8, 6, DateTimeKind.Utc);
        var spotifyCatalogueRelease = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc);

        var existing = new Episode
        {
            Id = ExistingId,
            PodcastId = podcast.Id,
            Title = "I Confronted My Ab*ser 30 Years Later. Everything Changed",
            Release = youTubeRelease,
            Length = TimeSpan.Parse("01:28:37"),
            YouTubeId = "UsqC0L9He2g",
            SpotifyId = SpotifyId,
            Urls = new ServiceUrls
            {
                YouTube = new Uri("https://www.youtube.com/watch?v=UsqC0L9He2g"),
                Spotify = new Uri($"https://open.spotify.com/episode/{SpotifyId}")
            }
        };
        var incoming = Episode.FromSpotify(
            SpotifyId,
            "I Confronted My Abuser 30 Years Later… Everything Changed",
            "description",
            TimeSpan.Parse("01:31:59.6990000"),
            false,
            spotifyCatalogueRelease,
            new Uri($"https://open.spotify.com/episode/{SpotifyId}"),
            null);

        var sut = new EpisodeMerger(new EpisodeMatcher(NullLogger<EpisodeMatcher>.Instance));
        var result = sut.MergeEpisodes(podcast, [existing], [incoming]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty("no fields changed when Spotify catalogue date is newer than YouTube publish");
        existing.Release.Should().Be(youTubeRelease);
    }

    [Fact]
    public void MergeEpisodes_WhenSpotifyOnlyReindexSameDateWithTime_DoesNotBackfillYouTubeReleaseTime()
    {
        var podcast = CreatePodcast();
        var youTubeRelease = new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc);
        var spotifyReleaseWithTime = new DateTime(2026, 7, 2, 8, 0, 0, DateTimeKind.Utc);

        var existing = new Episode
        {
            Id = ExistingId,
            PodcastId = podcast.Id,
            Title = "I Confronted My Ab*ser 30 Years Later. Everything Changed",
            Release = youTubeRelease,
            Length = TimeSpan.Parse("01:28:37"),
            YouTubeId = "UsqC0L9He2g",
            SpotifyId = SpotifyId,
            Urls = new ServiceUrls
            {
                YouTube = new Uri("https://www.youtube.com/watch?v=UsqC0L9He2g"),
                Spotify = new Uri($"https://open.spotify.com/episode/{SpotifyId}")
            }
        };
        var incoming = Episode.FromSpotify(
            SpotifyId,
            "I Confronted My Abuser 30 Years Later… Everything Changed",
            "description",
            TimeSpan.Parse("01:31:59.6990000"),
            false,
            spotifyReleaseWithTime,
            new Uri($"https://open.spotify.com/episode/{SpotifyId}"),
            null);

        var sut = new EpisodeMerger(new EpisodeMatcher(NullLogger<EpisodeMatcher>.Instance));
        var result = sut.MergeEpisodes(podcast, [existing], [incoming]);

        result.MergedEpisodes.Should().BeEmpty("Spotify-only merge must not backfill time on same date");
        existing.Release.Should().Be(youTubeRelease);
    }
}
