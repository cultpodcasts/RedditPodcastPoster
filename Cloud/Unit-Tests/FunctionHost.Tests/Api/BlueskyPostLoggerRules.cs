using FluentAssertions;
using RedditPodcastPoster.Bluesky.Logging;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;

namespace FunctionHost.Tests.Api;

public class BlueskyPostLoggerRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A Bluesky post message carries the stable prefix plus episode, podcast, caller and the catalog URL that is posted, because App Insights answers post provenance by searching that one line and a Netflix-only episode can be the posted URL.")]
    public void posted_message_carries_provenance_and_posted_catalog_url()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var youTubeId = _fixture.CreateYouTubeId();
        var youTubeUrl = _fixture.DefaultYouTubeUrl(youTubeId);
        var netflixUrl = new Uri($"https://www.netflix.com/title/{Math.Abs(_fixture.Create<int>())}");
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            EpisodeServicePresence.SetYouTubeIdentity(e, youTubeId);
            EpisodeServicePresence.Upsert(e, ServiceKeys.YouTube, youTubeUrl, null);
            EpisodeServicePresence.Upsert(e, ServiceKeys.Netflix, netflixUrl, null);
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
        message.Should().Contain($"posted-url='{youTubeUrl}'");
        message.Should().Contain($"posted-service='{ServiceKeys.YouTube}'");
        message.Should().Contain($"{ServiceKeys.YouTube}={youTubeUrl}");
        message.Should().Contain($"{ServiceKeys.Netflix}={netflixUrl}");
    }

    [Fact(DisplayName =
        "When YouTube, Spotify, and Apple are absent, the Bluesky post message logs the Netflix catalog URL as posted-url, because that is the URL known to be relevant.")]
    public void posted_message_logs_netflix_when_that_is_the_posted_url()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var netflixUrl = new Uri($"https://www.netflix.com/title/{Math.Abs(_fixture.Create<int>())}");
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            EpisodeServicePresence.Upsert(e, ServiceKeys.Netflix, netflixUrl, null);
        });
        var caller = _fixture.Create<string>();

        // Act
        var message = BlueskyPostLogger.FormatPostedMessage(new PodcastEpisode(podcast, episode), caller);

        // Assert
        message.Should().Contain($"posted-url='{netflixUrl}'");
        message.Should().Contain($"posted-service='{ServiceKeys.Netflix}'");
        message.Should().Contain($"{ServiceKeys.Netflix}={netflixUrl}");
    }
}
