using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.SocialPosting.Episodes;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

public class PodcastEpisodeFilterRules
{
    private readonly DomainTestFixture _fixture = new();

    private PodcastEpisodeFilter CreateSut() =>
        new(
            Options.Create(new DelayedYouTubePublication { EvaluationThreshold = TimeSpan.FromDays(7) }),
            NullLogger<PodcastEpisodeFilter>.Instance);

    [Fact(DisplayName =
        "YouTube release-authority episodes with Spotify but no YouTube URL are not Bluesky-ready.")]
    public async Task youtube_ra_spotify_only_not_bluesky_ready()
    {
        // Arrange
        var sut = CreateSut();
        var podcast = CreateYouTubeAuthorityPodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(
            podcast,
            release: DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay()));

        // Act
        var ready = await sut.GetMostRecentBlueskyReadyEpisodes(podcast, [episode], numberOfDays: 7);

        // Assert
        ready.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "YouTube release-authority episodes with a YouTube URL are Bluesky-ready.")]
    public async Task youtube_ra_with_youtube_url_is_bluesky_ready()
    {
        // Arrange
        var sut = CreateSut();
        var podcast = CreateYouTubeAuthorityPodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast,
            release: DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay()));

        // Act
        var ready = await sut.GetMostRecentBlueskyReadyEpisodes(podcast, [episode], numberOfDays: 7);

        // Assert
        ready.Should().ContainSingle().Which.Episode.Id.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "Non-YouTube release-authority episodes remain Bluesky-ready with Spotify-only URLs.")]
    public async Task spotify_ra_spotify_only_is_bluesky_ready()
    {
        // Arrange
        var sut = CreateSut();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(
            podcast,
            release: DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay()));

        // Act
        var ready = await sut.GetMostRecentBlueskyReadyEpisodes(podcast, [episode], numberOfDays: 7);

        // Assert
        ready.Should().ContainSingle().Which.Episode.Id.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "Episodes with a stored BlueskyPost AT URI are not Bluesky-ready.")]
    public async Task bluesky_post_uri_excludes_from_bluesky_ready()
    {
        // Arrange
        var sut = CreateSut();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(
            podcast,
            release: DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay()));
        episode.BlueskyPost = "at://did:plc:example/app.bsky.feed.post/3k2yuhir2j2";

        // Act
        var ready = await sut.GetMostRecentBlueskyReadyEpisodes(podcast, [episode], numberOfDays: 7);

        // Assert
        ready.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Episodes with legacy OldBlueskyPosted true are not Bluesky-ready.")]
    public async Task legacy_old_bluesky_posted_excludes_from_bluesky_ready()
    {
        // Arrange
        var sut = CreateSut();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(
            podcast,
            release: DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay()));
        episode.OldBlueskyPosted = true;

        // Act
        var ready = await sut.GetMostRecentBlueskyReadyEpisodes(podcast, [episode], numberOfDays: 7);

        // Assert
        ready.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "YouTube release-authority episodes with Spotify but no YouTube URL are not tweet-ready.")]
    public async Task youtube_ra_spotify_only_not_tweet_ready()
    {
        // Arrange
        var sut = CreateSut();
        var podcast = CreateYouTubeAuthorityPodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(
            podcast,
            release: DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay()));
        episode.OldTweeted = false;

        // Act
        var ready = await sut.GetMostRecentUntweetedEpisodes(podcast, [episode], numberOfDays: 7);

        // Assert
        ready.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When an episode has only TweetSkipReason set, then it is excluded from untweeted candidates, because a later hourly run must not rewrite the shortener for a permanent form failure.")]
    public async Task tweet_skip_reason_only_excludes_from_untweeted()
    {
        // Arrange
        var sut = CreateSut();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var skipReason = _fixture.Create<string>();
        var skipped = _fixture.CreateStoredEpisodeWithSpotifyOnly(
            podcast,
            release: DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay()));
        skipped.TweetSkipReason = skipReason;
        skipped.OldTweeted = false;
        skipped.TweetPost = null;
        var eligible = _fixture.CreateStoredEpisodeWithSpotifyOnly(
            podcast,
            release: DomainTestFixture.UtcAtTime(-1, _fixture.CreateNonMidnightTimeOfDay()));
        eligible.OldTweeted = false;
        eligible.TweetPost = null;

        // Act
        var ready = await sut.GetMostRecentUntweetedEpisodes(
            podcast, [skipped, eligible], numberOfDays: 7);

        // Assert
        ready.Should().ContainSingle().Which.Episode.Id.Should().Be(eligible.Id);
        skipped.Tweeted.Should().BeTrue();
        skipped.OldTweeted.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When an audio-first (Spotify-authority) podcast has a positive YouTube publishing delay and its Spotify-only episode " +
        "is still inside release + offset, it is not tweet-ready, because audio-first shows keep waiting for the delayed YouTube upload (#1375).")]
    public async Task audio_first_positive_delay_spotify_only_inside_window_not_tweet_ready()
    {
        // Arrange
        var sut = CreateSut();
        var podcast = CreateAudioFirstPodcastWithPositiveDelay();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, release: DateTime.UtcNow.AddHours(-1));
        episode.OldTweeted = false;

        // Act
        var ready = await sut.GetMostRecentUntweetedEpisodes(podcast, [episode], numberOfDays: 7);

        // Assert
        ready.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When an audio-first (Spotify-authority) podcast has a positive YouTube publishing delay and its Spotify-only episode " +
        "is still inside release + offset, it is not Bluesky-ready, because audio-first shows keep waiting for the delayed YouTube upload (#1375).")]
    public async Task audio_first_positive_delay_spotify_only_inside_window_not_bluesky_ready()
    {
        // Arrange
        var sut = CreateSut();
        var podcast = CreateAudioFirstPodcastWithPositiveDelay();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, release: DateTime.UtcNow.AddHours(-1));

        // Act
        var ready = await sut.GetMostRecentBlueskyReadyEpisodes(podcast, [episode], numberOfDays: 7);

        // Assert
        ready.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When an audio-first (Spotify-authority) podcast's positive YouTube publishing delay has passed, its Spotify-only episode " +
        "is tweet-ready, because the hold only lasts until release + offset.")]
    public async Task audio_first_positive_delay_spotify_only_after_window_is_tweet_ready()
    {
        // Arrange
        var sut = CreateSut();
        var podcast = CreateAudioFirstPodcastWithPositiveDelay();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, release: DateTime.UtcNow.AddDays(-2));
        episode.OldTweeted = false;

        // Act
        var ready = await sut.GetMostRecentUntweetedEpisodes(podcast, [episode], numberOfDays: 7);

        // Assert
        ready.Should().ContainSingle().Which.Episode.Id.Should().Be(episode.Id);
    }

    [Fact(DisplayName =
        "When an audio-first (Spotify-authority) podcast's positive YouTube publishing delay has passed, its Spotify-only episode " +
        "is Bluesky-ready, because the hold only lasts until release + offset.")]
    public async Task audio_first_positive_delay_spotify_only_after_window_is_bluesky_ready()
    {
        // Arrange
        var sut = CreateSut();
        var podcast = CreateAudioFirstPodcastWithPositiveDelay();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, release: DateTime.UtcNow.AddDays(-2));

        // Act
        var ready = await sut.GetMostRecentBlueskyReadyEpisodes(podcast, [episode], numberOfDays: 7);

        // Assert
        ready.Should().ContainSingle().Which.Episode.Id.Should().Be(episode.Id);
    }

    private Podcast CreateAudioFirstPodcastWithPositiveDelay()
    {
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        podcast.YouTubePublicationOffset = TimeSpan.FromHours(6).Ticks;
        return podcast;
    }

    private Podcast CreateYouTubeAuthorityPodcast() =>        _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
}
