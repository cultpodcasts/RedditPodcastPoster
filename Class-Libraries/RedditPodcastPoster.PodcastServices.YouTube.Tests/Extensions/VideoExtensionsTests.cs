using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;

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

    [Fact]
    public void GetThumbnailCandidates_OrdersByHeightDescending()
    {
        var video = new Google.Apis.YouTube.v3.Data.Video
        {
            Snippet = new VideoSnippet
            {
                Thumbnails = new ThumbnailDetails
                {
                    Maxres = new Thumbnail { Url = "https://example.com/maxres.jpg", Height = 720, Width = 1280 },
                    Standard = new Thumbnail { Url = "https://example.com/sd.jpg", Height = 480, Width = 640 },
                    Default__ = new Thumbnail { Url = "https://example.com/default.jpg", Height = 90, Width = 120 }
                }
            }
        };

        var candidates = video.GetThumbnailCandidates();

        candidates.Should().HaveCount(3);
        candidates[0].Height.Should().Be(720);
        candidates[0].IsDefaultTier.Should().BeFalse();
        candidates[^1].IsDefaultTier.Should().BeTrue();
    }
}
