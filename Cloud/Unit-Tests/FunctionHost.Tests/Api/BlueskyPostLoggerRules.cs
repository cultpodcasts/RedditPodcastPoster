using FluentAssertions;
using RedditPodcastPoster.Bluesky.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;

namespace FunctionHost.Tests.Api;

public class BlueskyPostLoggerRules
{
    [Fact(DisplayName = "FormatPostedMessage uses stable Bluesky posted: prefix and includes ids/urls.")]
    public void format_posted_message_includes_provenance_and_urls()
    {
        var episodeId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var podcastId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var podcast = new Podcast
        {
            Id = podcastId,
            Name = "Preacher Boys Podcast"
        };
        var episode = new Episode
        {
            Id = episodeId,
            PodcastId = podcastId,
            Title = "Pastor Kenny Baldwin",
            Urls = new ServiceUrls
            {
                Spotify = new Uri("https://open.spotify.com/episode/spotifyEp123"),
                YouTube = new Uri("https://www.youtube.com/watch?v=ytVid456"),
                Apple = new Uri("https://podcasts.apple.com/us/podcast/id1?i=9876543210")
            }
        };
        var podcastEpisode = new PodcastEpisode(podcast, episode);

        var message = BlueskyPostLogger.FormatPostedMessage(
            podcastEpisode,
            caller: "BlueskyPoster.Post");

        message.Should().StartWith(BlueskyPostLogger.PostedMessagePrefix);
        message.Should().Contain($"episode-id='{episodeId}'");
        message.Should().Contain("title='Pastor Kenny Baldwin'");
        message.Should().Contain($"podcast-id='{podcastId}'");
        message.Should().Contain("podcast-name='Preacher Boys Podcast'");
        message.Should().Contain("caller='BlueskyPoster.Post'");
        message.Should().Contain("spotify-url='https://open.spotify.com/episode/spotifyEp123'");
        message.Should().Contain("youtube-url='https://www.youtube.com/watch?v=ytVid456'");
        message.Should().Contain("apple-url='https://podcasts.apple.com/us/podcast/id1?i=9876543210'");
    }
}
