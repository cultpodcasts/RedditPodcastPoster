using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Paging;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Paging;

public class YouTubeChannelVideoSnippetsServicePagingRules
{
    [Fact(DisplayName =
        "A channel search walk stops when search.list hands back the token it was just asked for and keeps " +
        "the results so far, because each repeated call costs 100 quota units. With no reliable total, an empty " +
        "repeating page is treated as a complete read and logs at Warning.")]
    public async Task Repeating_search_token_terminates()
    {
        // Arrange
        var api = new FakeYouTubePagingApi()
            .Page("", new FakeYouTubePage(FakeYouTubePagingApi.Ids("s", 0, 5), "t2"))
            .Page("t2", new FakeYouTubePage(FakeYouTubePagingApi.Ids("s", 5, 3), "t3"))
            .Page("t3", new FakeYouTubePage([], "t3"));
        var wrapper = new Mock<IYouTubeServiceWrapper>();
        wrapper.SetupGet(x => x.YouTubeService).Returns(api.CreateService());
        var logger = new RecordingLogger<YouTubeChannelVideoSnippetsService>();
        var sut = new YouTubeChannelVideoSnippetsService(Mock.Of<IYouTubeQuotaUsageTracker>(), logger);

        // Act
        var result = await sut.GetLatestChannelVideoSnippets(wrapper.Object,
            new YouTubeChannelId("UCsyntheticchannel0001"), new IndexingContext());

        // Assert
        api.Requests.Should().HaveCount(3);
        result.Should().HaveCount(8);
        logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Warning &&
            e.Message.StartsWith(YouTubePageWalkGuard.StoppedMessagePrefix) &&
            e.Message.Contains(nameof(YouTubePageWalkStopReason.RepeatedPageToken)));
    }

    [Fact(DisplayName =
        "A channel search page of only already-seen ids ends the walk at Warning, because search.list repeats " +
        "results across 5-item pages and the stop is a cost guard, not a fault.")]
    public async Task All_seen_search_page_stops_at_warning()
    {
        // Arrange
        var ids = FakeYouTubePagingApi.Ids("s", 0, 5);
        var api = new FakeYouTubePagingApi()
            .Page("", new FakeYouTubePage(ids, "t2"))
            .Page("t2", new FakeYouTubePage(ids, "t3"))
            .Page("t3", new FakeYouTubePage(FakeYouTubePagingApi.Ids("s", 5, 5), null));
        var wrapper = new Mock<IYouTubeServiceWrapper>();
        wrapper.SetupGet(x => x.YouTubeService).Returns(api.CreateService());
        var logger = new RecordingLogger<YouTubeChannelVideoSnippetsService>();
        var sut = new YouTubeChannelVideoSnippetsService(Mock.Of<IYouTubeQuotaUsageTracker>(), logger);

        // Act
        var result = await sut.GetLatestChannelVideoSnippets(wrapper.Object,
            new YouTubeChannelId("UCsyntheticchannel0002"), new IndexingContext());

        // Assert
        api.Requests.Should().HaveCount(2);
        result.Should().HaveCount(10);
        logger.Entries.Should().ContainSingle(e =>
            e.Level == LogLevel.Warning &&
            e.Message.Contains(nameof(YouTubePageWalkStopReason.NoNewItems)));
    }
}
