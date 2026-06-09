using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Extensions;

public class PlaylistItemExtensionsTests
{
    [Fact]
    public void ForEpisodeMatching_WithoutReleasedSince_CapsToFiveItems()
    {
        var items = Enumerable.Range(1, 10)
            .Select(i => CreatePlaylistItem($"video-{i}"))
            .ToList();

        var result = items.ForEpisodeMatching(new IndexingContext());

        result.Should().HaveCount(5);
        result.Select(x => x.GetVideoId()).Should().Equal("video-1", "video-2", "video-3", "video-4", "video-5");
    }

    [Fact]
    public void ForEpisodeMatching_WithReleasedSince_FiltersByPublishDate()
    {
        var releasedSince = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var items = new List<PlaylistItem>
        {
            CreatePlaylistItem("old", releasedSince.AddDays(-1)),
            CreatePlaylistItem("new", releasedSince.AddDays(1))
        };

        var result = items.ForEpisodeMatching(new IndexingContext(releasedSince));

        result.Should().ContainSingle().Which.GetVideoId().Should().Be("new");
    }

    [Fact]
    public void GetVideoId_PrefersContentDetailsVideoId()
    {
        var item = new PlaylistItem
        {
            ContentDetails = new PlaylistItemContentDetails { VideoId = "content-details-id" },
            Snippet = new PlaylistItemSnippet
            {
                ResourceId = new ResourceId { VideoId = "resource-id" }
            }
        };

        item.GetVideoId().Should().Be("content-details-id");
    }

    private static PlaylistItem CreatePlaylistItem(string videoId, DateTimeOffset? publishedAt = null) =>
        new()
        {
            Snippet = new PlaylistItemSnippet
            {
                ResourceId = new ResourceId { VideoId = videoId },
                PublishedAtDateTimeOffset = publishedAt ?? DateTimeOffset.UtcNow
            }
        };
}
