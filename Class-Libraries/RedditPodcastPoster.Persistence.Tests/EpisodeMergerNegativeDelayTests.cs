using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;

namespace RedditPodcastPoster.Persistence.Tests;

public class EpisodeMergerNegativeDelayTests
{
    private static readonly Guid PodcastId = Guid.Parse("1aa72d3d-f1e4-458f-a172-62990ef6c200");
    private static readonly TimeSpan YouTubeFirstDelay = TimeSpan.FromDays(-33).Add(TimeSpan.FromHours(-12));

    private readonly EpisodeMerger _sut = new(new EpisodeMatcher(NullLogger<EpisodeMatcher>.Instance));

    [Fact]
    public void MergeEpisodes_WhenSpotifyIncomingMatchesExistingOwnerById_MergesOntoOwnerNotYouTubeOnlyEpisode()
    {
        var podcast = new Podcast
        {
            Id = PodcastId,
            Name = "Cults to Consciousness",
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = YouTubeFirstDelay.Ticks
        };

        var correctOwnerId = Guid.Parse("1c804814-12ac-40c8-a223-88ab7c703d38");
        var wrongYouTubeOnlyId = Guid.Parse("53ba0c64-58a7-4292-b7fe-ba135d4d3160");
        const string otoSpotifyId = "16LveQifI6eBwDXAINpd7G";
        var otoSpotifyUrl = new Uri($"https://open.spotify.com/episode/{otoSpotifyId}");

        var correctOwner = new Episode
        {
            Id = correctOwnerId,
            PodcastId = PodcastId,
            Title = "What Really Happens During \"Ordo Templi Orientis\" Initiations?  (Trapped in a Secret Society)",
            Release = new DateTime(2026, 5, 20, 22, 15, 16, DateTimeKind.Utc),
            Length = TimeSpan.FromMinutes(61) + TimeSpan.FromSeconds(42),
            SpotifyId = otoSpotifyId,
            YouTubeId = "l3aIdJeg0vE",
            Urls = new ServiceUrls
            {
                Spotify = otoSpotifyUrl,
                YouTube = new Uri("https://www.youtube.com/watch?v=l3aIdJeg0vE")
            }
        };

        var wrongYouTubeOnly = new Episode
        {
            Id = wrongYouTubeOnlyId,
            PodcastId = PodcastId,
            Title = "Why He Thinks Daughters Should Parent Their Siblings  (ft. Tia Levings)",
            Release = new DateTime(2026, 5, 31, 21, 15, 27, DateTimeKind.Utc),
            Length = TimeSpan.FromMinutes(61) + TimeSpan.FromSeconds(35),
            YouTubeId = "u6ZF-2sWQQc",
            Urls = new ServiceUrls { YouTube = new Uri("https://www.youtube.com/watch?v=u6ZF-2sWQQc") }
        };

        var incoming = Episode.FromSpotify(
            otoSpotifyId,
            "What Really Happens During Ordo Templi Orientis Initiations",
            "Incoming description",
            TimeSpan.FromMinutes(61) + TimeSpan.FromSeconds(42),
            false,
            new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc),
            otoSpotifyUrl,
            null);

        var result = _sut.MergeEpisodes(podcast, [correctOwner, wrongYouTubeOnly], [incoming]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty(
            "Spotify re-index must not rewrite YouTube release when catalogue date is newer");
        correctOwner.Release.Should().Be(new DateTime(2026, 5, 20, 22, 15, 16, DateTimeKind.Utc));
        wrongYouTubeOnly.SpotifyId.Should().BeNullOrEmpty();
    }

    [Fact]
    public void MergeEpisodes_WhenNegativeDelayAndTitlesDiffer_DoesNotMergeByReleaseAndDurationAlone()
    {
        var podcast = new Podcast
        {
            Id = PodcastId,
            Name = "Cults to Consciousness",
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = YouTubeFirstDelay.Ticks
        };

        var existing = new Episode
        {
            Id = Guid.Parse("53ba0c64-58a7-4292-b7fe-ba135d4d3160"),
            PodcastId = PodcastId,
            Title = "Why He Thinks Daughters Should Parent Their Siblings  (ft. Tia Levings)",
            Release = new DateTime(2026, 5, 31, 21, 15, 27, DateTimeKind.Utc),
            Length = TimeSpan.FromMinutes(61) + TimeSpan.FromSeconds(35),
            YouTubeId = "u6ZF-2sWQQc",
            Urls = new ServiceUrls { YouTube = new Uri("https://www.youtube.com/watch?v=u6ZF-2sWQQc") }
        };

        var incoming = Episode.FromSpotify(
            "1BTQKaev5KLjScdwHII14B",
            "Becoming a Fundamentalist Trad Wife Almost Killed Me",
            "Incoming description",
            TimeSpan.FromMinutes(61) + TimeSpan.FromSeconds(30),
            false,
            new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc),
            new Uri("https://open.spotify.com/episode/1BTQKaev5KLjScdwHII14B"),
            null);

        var result = _sut.MergeEpisodes(podcast, [existing], [incoming]);

        result.MergedEpisodes.Should().BeEmpty();
        result.AddedEpisodes.Should().ContainSingle();
        existing.SpotifyId.Should().BeNullOrEmpty();
    }

    [Fact]
    public void MergeEpisodes_WhenYouTubeIncomingSameDateWithTime_BackfillsMidnightRelease()
    {
        var podcast = new Podcast { Id = PodcastId, Name = "Test Podcast" };
        var dateOnlyRelease = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var youTubeRelease = new DateTime(2026, 7, 1, 12, 30, 0, DateTimeKind.Utc);
        var existing = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = PodcastId,
            Title = "Episode title",
            Release = dateOnlyRelease,
            Length = TimeSpan.FromMinutes(45),
            SpotifyId = SpotifyEpisodeId,
            Urls = new ServiceUrls { Spotify = SpotifyUrl }
        };
        var incoming = Episode.FromYouTube(
            "video-id",
            "Episode title",
            "YouTube description",
            TimeSpan.FromMinutes(45),
            false,
            youTubeRelease,
            new Uri("https://www.youtube.com/watch?v=video-id"),
            null);

        var result = _sut.MergeEpisodes(podcast, [existing], [incoming]);

        result.MergedEpisodes.Should().ContainSingle();
        existing.Release.Should().Be(youTubeRelease);
    }

    [Fact]
    public void MergeEpisodes_WhenSpotifyOnlyIncomingSameDateWithTime_DoesNotBackfillMidnightRelease()
    {
        var podcast = new Podcast { Id = PodcastId, Name = "Test Podcast" };
        var dateOnlyRelease = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var spotifyRelease = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc);
        var existing = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = PodcastId,
            Title = "Episode title",
            Release = dateOnlyRelease,
            Length = TimeSpan.FromMinutes(45),
            SpotifyId = SpotifyEpisodeId,
            Urls = new ServiceUrls { Spotify = SpotifyUrl }
        };
        var incoming = Episode.FromSpotify(
            SpotifyEpisodeId,
            "Episode title",
            "Incoming description",
            TimeSpan.FromMinutes(45),
            false,
            spotifyRelease,
            SpotifyUrl,
            null);

        var result = _sut.MergeEpisodes(podcast, [existing], [incoming]);

        result.MergedEpisodes.Should().BeEmpty("Spotify catalogue merge must not backfill time-of-day");
        existing.Release.Should().Be(dateOnlyRelease);
    }

    [Fact]
    public void MergeEpisodes_WhenYouTubeIncomingDifferentDateWithTime_DoesNotBackfillMidnightRelease()
    {
        var podcast = new Podcast { Id = PodcastId, Name = "Test Podcast" };
        var dateOnlyRelease = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var youTubeRelease = new DateTime(2026, 7, 2, 12, 30, 0, DateTimeKind.Utc);
        var existing = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = PodcastId,
            Title = "Episode title",
            Release = dateOnlyRelease,
            Length = TimeSpan.FromMinutes(45),
            SpotifyId = SpotifyEpisodeId,
            Urls = new ServiceUrls { Spotify = SpotifyUrl }
        };
        var incoming = Episode.FromYouTube(
            "video-id",
            "Episode title",
            "YouTube description",
            TimeSpan.FromMinutes(45),
            false,
            youTubeRelease,
            new Uri("https://www.youtube.com/watch?v=video-id"),
            null);

        var result = _sut.MergeEpisodes(podcast, [existing], [incoming]);

        result.MergedEpisodes.Should().ContainSingle();
        existing.Release.Should().Be(dateOnlyRelease, "time backfill requires same UTC calendar date");
    }

    private const string SpotifyEpisodeId = "1UncRhHtmojlTq2mO0Gntz";
    private static readonly Uri SpotifyUrl = new($"https://open.spotify.com/episode/{SpotifyEpisodeId}");
}
