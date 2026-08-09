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
        episode.Tweeted = false;

        // Act
        var ready = await sut.GetMostRecentUntweetedEpisodes(podcast, [episode], numberOfDays: 7);

        // Assert
        ready.Should().BeEmpty();
    }

    private Podcast CreateYouTubeAuthorityPodcast() =>
        _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
}
