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
    public void IsMembersOnly_WhenViewCountAbsentButOtherStatisticsPresent_ReturnsTrue()
    {
        // Members-only videos stay publicly listed but YouTube omits statistics.viewCount for them,
        // while still returning likeCount/favoriteCount/commentCount.
        var video = new Google.Apis.YouTube.v3.Data.Video
        {
            Statistics = new VideoStatistics
            {
                LikeCount = 0,
                FavoriteCount = 0,
                CommentCount = 0
                // ViewCount deliberately left null (property absent) — the members-only signal.
            }
        };

        video.IsMembersOnly().Should().BeTrue();
    }

    [Fact]
    public void IsMembersOnly_WhenViewCountIsZero_ReturnsFalse()
    {
        // A brand-new PUBLIC upload with no views returns "viewCount": "0" (present, value 0).
        // Zero views must NOT be treated as members-only.
        var video = new Google.Apis.YouTube.v3.Data.Video
        {
            Statistics = new VideoStatistics
            {
                ViewCount = 0,
                LikeCount = 0,
                CommentCount = 0
            }
        };

        video.IsMembersOnly().Should().BeFalse();
    }

    [Fact]
    public void IsMembersOnly_WhenViewCountPresent_ReturnsFalse()
    {
        var video = new Google.Apis.YouTube.v3.Data.Video
        {
            Statistics = new VideoStatistics { ViewCount = 4242 }
        };

        video.IsMembersOnly().Should().BeFalse();
    }

    [Fact]
    public void IsMembersOnly_WhenStatisticsNotRequested_ReturnsFalse()
    {
        // Statistics part not requested -> Statistics is null -> never flag as members-only.
        var video = new Google.Apis.YouTube.v3.Data.Video { Statistics = null };

        video.IsMembersOnly().Should().BeFalse();
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
