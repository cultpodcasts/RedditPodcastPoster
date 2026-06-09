using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Extensions;

public class VideoExtensionsTests
{
    [Theory]
    [InlineData("none", true)]
    [InlineData("completed", true)]
    [InlineData("live", false)]
    [InlineData("upcoming", false)]
    public void IsCompletedPublicVideo_FiltersLiveAndUpcoming(string liveBroadcastContent, bool expected)
    {
        var video = new Google.Apis.YouTube.v3.Data.Video
        {
            Snippet = new VideoSnippet { LiveBroadcastContent = liveBroadcastContent }
        };

        video.IsCompletedPublicVideo().Should().Be(expected);
    }
}
