using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Extensions;

/// <summary>
/// A playlist item's <c>snippet.publishedAt</c> is the added-to-playlist time. A scheduled upload
/// joins the playlist when it is uploaded, days before it becomes public, so windowing purely on
/// added-at drops the episode on the day it actually releases.
/// </summary>
public class ScheduledUploadIndexingWindowRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When a playlist item's video published after the item was added to the playlist, the indexing window " +
        "date is the video's publication, because a scheduled upload joins the playlist before it goes public.")]
    public void Scheduled_upload_is_windowed_on_video_publication()
    {
        // Arrange
        var addedAt = DomainTestFixture.UtcAtTime(-5, TimeSpan.FromHours(14));
        var videoPublishedAt = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(15));
        var item = CreatePlaylistItem(_fixture.CreateYouTubeId(), addedAt, videoPublishedAt);

        // Act
        var windowDate = item.GetIndexingWindowDate();

        // Assert
        windowDate.Should().Be(new DateTimeOffset(videoPublishedAt, TimeSpan.Zero));
    }

    [Fact(DisplayName =
        "When a backlog video is added to a curated playlist long after it was published, the indexing window " +
        "date stays the added-to-playlist time, because added-at is the new-to-this-feed signal.")]
    public void Backlog_video_is_windowed_on_added_to_playlist_time()
    {
        // Arrange
        var videoPublishedAt = DomainTestFixture.UtcAtTime(-400, TimeSpan.FromHours(9));
        var addedAt = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(11));
        var item = CreatePlaylistItem(_fixture.CreateYouTubeId(), addedAt, videoPublishedAt);

        // Act
        var windowDate = item.GetIndexingWindowDate();

        // Assert
        windowDate.Should().Be(new DateTimeOffset(addedAt, TimeSpan.Zero));
    }

    [Fact(DisplayName =
        "When a playlist item carries no content details, the indexing window date falls back to the " +
        "added-to-playlist time, because video-published-at is only returned when contentDetails were requested.")]
    public void Missing_content_details_falls_back_to_added_to_playlist_time()
    {
        // Arrange
        var addedAt = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(8));
        var item = CreatePlaylistItem(_fixture.CreateYouTubeId(), addedAt, videoPublishedAt: null);

        // Act
        var windowDate = item.GetIndexingWindowDate();

        // Assert
        windowDate.Should().Be(new DateTimeOffset(addedAt, TimeSpan.Zero));
    }

    [Fact(DisplayName =
        "When episode matching is scoped by released-since, a scheduled upload added to the playlist before the " +
        "window but published inside it is retained, because the video is new to the catalogue today.")]
    public void Episode_matching_retains_scheduled_upload_published_inside_the_window()
    {
        // Arrange
        var releasedSince = DomainTestFixture.UtcDaysAgo(2);
        var scheduledId = _fixture.CreateYouTubeId();
        var staleId = _fixture.CreateYouTubeId();
        var items = new List<PlaylistItem>
        {
            CreatePlaylistItem(
                scheduledId,
                addedAt: DomainTestFixture.UtcAtTime(-5, TimeSpan.FromHours(14)),
                videoPublishedAt: DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(15))),
            CreatePlaylistItem(
                staleId,
                addedAt: DomainTestFixture.UtcAtTime(-5, TimeSpan.FromHours(14)),
                videoPublishedAt: DomainTestFixture.UtcAtTime(-5, TimeSpan.FromHours(15)))
        };

        // Act
        var matched = items.ForEpisodeMatching(new IndexingContext(releasedSince));

        // Assert
        matched.Should().ContainSingle().Which.GetVideoId().Should().Be(scheduledId);
    }

    private static PlaylistItem CreatePlaylistItem(
        string videoId,
        DateTime addedAt,
        DateTime? videoPublishedAt) =>
        new()
        {
            Snippet = new PlaylistItemSnippet
            {
                ResourceId = new ResourceId { VideoId = videoId },
                PublishedAtDateTimeOffset = new DateTimeOffset(addedAt, TimeSpan.Zero)
            },
            ContentDetails = videoPublishedAt == null
                ? null
                : new PlaylistItemContentDetails
                {
                    VideoId = videoId,
                    VideoPublishedAtDateTimeOffset = new DateTimeOffset(videoPublishedAt.Value, TimeSpan.Zero)
                }
        };
}
