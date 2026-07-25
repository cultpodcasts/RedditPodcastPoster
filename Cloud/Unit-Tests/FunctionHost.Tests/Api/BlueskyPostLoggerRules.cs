using FluentAssertions;
using RedditPodcastPoster.Bluesky.Logging;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using Xunit;

namespace FunctionHost.Tests.Api;

public class BlueskyPostLoggerRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A Bluesky post message carries the stable prefix plus episode, podcast, caller and every platform url " +
        "because App Insights answers post provenance by searching that one line.")]
    public void posted_message_carries_provenance_and_platform_urls()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var spotifyId = _fixture.CreateSpotifyId();
        var youTubeId = _fixture.CreateYouTubeId();
        var appleId = _fixture.CreateAppleId();
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.SpotifyId = spotifyId;
            e.YouTubeId = youTubeId;
            e.AppleId = appleId;
            e.Urls.Spotify = _fixture.DefaultSpotifyUrl(spotifyId);
            e.Urls.YouTube = _fixture.DefaultYouTubeUrl(youTubeId);
            e.Urls.Apple = _fixture.DefaultAppleUrl(appleId);
        });
        var caller = _fixture.Create<string>();

        // Act
        var message = BlueskyPostLogger.FormatPostedMessage(new PodcastEpisode(podcast, episode), caller);

        // Assert
        message.Should().StartWith(BlueskyPostLogger.PostedMessagePrefix);
        message.Should().Contain($"episode-id='{episode.Id}'");
        message.Should().Contain($"title='{episode.Title}'");
        message.Should().Contain($"podcast-id='{podcast.Id}'");
        message.Should().Contain($"podcast-name='{podcast.Name}'");
        message.Should().Contain($"caller='{caller}'");
        message.Should().Contain($"spotify-url='{episode.Urls.Spotify}'");
        message.Should().Contain($"youtube-url='{episode.Urls.YouTube}'");
        message.Should().Contain($"apple-url='{episode.Urls.Apple}'");
    }
}
