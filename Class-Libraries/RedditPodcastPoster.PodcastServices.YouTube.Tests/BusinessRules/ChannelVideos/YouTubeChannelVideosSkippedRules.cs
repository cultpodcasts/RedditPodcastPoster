using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelVideos;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.ChannelVideos;

/// <summary>
/// Real case: 'The Cult on the Cape' (podcast 51d2f153-c2d0-4551-982d-9d1a405f3a80) is channel-only
/// (youTubeChannelId UC1hYXZi0_9KdI4z0qtSCIkQ, empty youTubePlaylistId). On 2026-10-15 00:03 BST an Indexer pass
/// ran with bypass-youtube True and logged "GetChannelVideos: Unable to find channel with id ..." and
/// "YouTube playlist '' ... was not found" although the channel is live. A bypassed pass must not report not-found.
/// </summary>
public class YouTubeChannelVideosSkippedRules
{
    private const string CultOnTheCapeChannelId = "UC1hYXZi0_9KdI4z0qtSCIkQ";

    private readonly Mock<IYouTubeServiceWrapper> _wrapper = new();
    private readonly Mock<ITolerantYouTubePlaylistService> _playlistService = new();
    private readonly RecordingLogger<YouTubeChannelVideosService> _logger = new();

    private YouTubeChannelVideosService CreateSut()
    {
        _wrapper.SetupGet(x => x.CanRotate).Returns(true);
        var quota = new Mock<IYouTubeQuotaUsageTracker>();
        var channelService = new YouTubeChannelService(
            _wrapper.Object, quota.Object, new RecordingLogger<YouTubeChannelService>());
        var tolerant = new TolerantYouTubeChannelService(
            _wrapper.Object, channelService, quota.Object, new RecordingLogger<TolerantYouTubeChannelService>());
        return new YouTubeChannelVideosService(tolerant, _playlistService.Object, _logger);
    }

    [Fact(DisplayName = "Cult on the Cape: bypassed YouTube pass does not report the channel as not found")]
    public async Task Bypassed_Pass_Does_Not_Report_NotFound()
    {
        var sut = CreateSut();
        var indexingContext = new IndexingContext(SkipYouTubeUrlResolving: true);

        var response = await sut.GetChannelVideos(new YouTubeChannelId(CultOnTheCapeChannelId), indexingContext);

        response.PlaylistItems.Should().BeNull();
        response.Failure.Should().NotBe(YouTubePlaylistFetchFailure.NotFound);
        response.Failure.Should().NotBe(YouTubePlaylistFetchFailure.ApiError);
        _logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Error);
        _wrapper.VerifyGet(x => x.YouTubeService, Times.Never);
        _playlistService.VerifyNoOtherCalls();
    }
}
