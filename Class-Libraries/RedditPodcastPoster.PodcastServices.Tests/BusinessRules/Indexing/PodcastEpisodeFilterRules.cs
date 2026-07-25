using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

public class PodcastEpisodeFilterRules
{
    private static PodcastEpisodeFilter CreateSut() =>
        new(
            Options.Create(new DelayedYouTubePublication { EvaluationThreshold = TimeSpan.FromDays(7) }),
            NullLogger<PodcastEpisodeFilter>.Instance);

    [Fact(DisplayName =
        "YouTube release-authority episodes with Spotify but no YouTube URL are not Bluesky-ready.")]
    public async Task youtube_ra_spotify_only_not_bluesky_ready()
    {
        var sut = CreateSut();
        var podcast = CreateYouTubeAuthorityPodcast();
        var episode = CreateRecentEpisode(e =>
        {
            e.Urls.Spotify = new Uri("https://open.spotify.com/episode/abc");
            e.Urls.YouTube = null;
        });

        var ready = await sut.GetMostRecentBlueskyReadyEpisodes(podcast, [episode], numberOfDays: 7);

        ready.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "YouTube release-authority episodes with a YouTube URL are Bluesky-ready.")]
    public async Task youtube_ra_with_youtube_url_is_bluesky_ready()
    {
        var sut = CreateSut();
        var podcast = CreateYouTubeAuthorityPodcast();
        var episode = CreateRecentEpisode(e =>
        {
            e.Urls.YouTube = new Uri("https://www.youtube.com/watch?v=abc");
            e.Urls.Spotify = null;
        });

        var ready = await sut.GetMostRecentBlueskyReadyEpisodes(podcast, [episode], numberOfDays: 7);

        ready.Should().ContainSingle().Which.Episode.Id.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "Non-YouTube release-authority episodes remain Bluesky-ready with Spotify-only URLs.")]
    public async Task spotify_ra_spotify_only_is_bluesky_ready()
    {
        var sut = CreateSut();
        var podcast = new Podcast
        {
            Id = Guid.NewGuid(),
            Name = "Audio Podcast",
            ReleaseAuthority = Service.Spotify,
            SpotifyId = "show123"
        };
        var episode = CreateRecentEpisode(e =>
        {
            e.Urls.Spotify = new Uri("https://open.spotify.com/episode/abc");
            e.Urls.YouTube = null;
        });

        var ready = await sut.GetMostRecentBlueskyReadyEpisodes(podcast, [episode], numberOfDays: 7);

        ready.Should().ContainSingle().Which.Episode.Id.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "YouTube release-authority episodes with Spotify but no YouTube URL are not tweet-ready.")]
    public async Task youtube_ra_spotify_only_not_tweet_ready()
    {
        var sut = CreateSut();
        var podcast = CreateYouTubeAuthorityPodcast();
        var episode = CreateRecentEpisode(e =>
        {
            e.Urls.Spotify = new Uri("https://open.spotify.com/episode/abc");
            e.Urls.YouTube = null;
            e.Tweeted = false;
        });

        var ready = await sut.GetMostRecentUntweetedEpisodes(podcast, [episode], numberOfDays: 7);

        ready.Should().BeEmpty();
    }

    private static Podcast CreateYouTubeAuthorityPodcast() =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "YouTube RA Podcast",
            ReleaseAuthority = Service.YouTube,
            YouTubeChannelId = "UCxxxxxxxxxxxxxxxxxxxxxx",
            SpotifyId = "show123",
            YouTubePublicationOffset = TimeSpan.FromDays(-4).Ticks
        };

    private static Episode CreateRecentEpisode(Action<Episode> customize)
    {
        var episode = new Episode
        {
            Id = Guid.NewGuid(),
            Title = "Recent episode",
            Release = DateTime.UtcNow.AddHours(-2),
            Length = TimeSpan.FromMinutes(40),
            Urls = new ServiceUrls()
        };
        customize(episode);
        return episode;
    }
}
