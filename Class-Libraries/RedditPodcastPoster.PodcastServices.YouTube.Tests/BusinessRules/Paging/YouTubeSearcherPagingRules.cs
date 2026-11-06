using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.YouTube.Tests.Paging;
using YouTubeChannel = Google.Apis.YouTube.v3.Data.Channel;
using YouTubeVideo = Google.Apis.YouTube.v3.Data.Video;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Channel;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Factories;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Paging;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Video;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.Paging;

public class YouTubeSearcherPagingRules
{
    private static readonly DateTime ReleasedSince = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly RecordingLogger<YouTubeSearcher> _logger = new();

    public YouTubeSearcherPagingRules()
    {
        _mocker.Use<ILogger<YouTubeSearcher>>(_logger);
        _mocker.GetMock<INoRedirectHttpClientFactory>()
            .Setup(factory => factory.Create())
            .Returns(new HttpClient());
        _mocker.GetMock<ITolerantYouTubeVideoService>()
            .Setup(service => service.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync((IList<YouTubeVideo>?)null);
        _mocker.GetMock<ITolerantYouTubeChannelService>()
            .Setup(service => service.GetChannel(
                It.IsAny<YouTubeChannelId>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync((YouTubeChannel?)null);
    }

    [Fact(DisplayName =
        "A query search stops after one extra call when search.list returns an empty page whose token repeats, " +
        "and keeps the videos already found, because each repeated call costs 100 quota units.")]
    public async Task repeating_empty_page_ends_the_search_and_keeps_earlier_results()
    {
        // Arrange
        var found = FakeYouTubePagingApi.Ids("q", 0, 3);
        var api = new FakeYouTubePagingApi()
            .Page("", new FakeYouTubePage(found, "repeat"))
            .Page("repeat", new FakeYouTubePage([], "repeat"));
        _mocker.GetMock<IYouTubeServiceWrapper>().SetupGet(wrapper => wrapper.YouTubeService).Returns(api.CreateService());
        var sut = _mocker.CreateInstance<YouTubeSearcher>();

        // Act
        var results = await sut.Search(_fixture.Create<string>(), new IndexingContext(ReleasedSince: ReleasedSince));

        // Assert
        api.Requests.Should().HaveCount(4);
        results.Select(result => result.Id).Distinct().Should().Equal(found);
        _logger.Entries.Should().Contain(entry =>
            entry.Message.StartsWith(YouTubePageWalkGuard.StoppedMessagePrefix) &&
            entry.Message.Contains(nameof(YouTubePageWalkStopReason.RepeatedPageToken)));
    }

    [Fact(DisplayName =
        "A query search still reads every page when each next-page token is new, because a normal dated search " +
        "must not stop early.")]
    public async Task normal_multi_page_search_reads_every_page()
    {
        // Arrange
        var first = FakeYouTubePagingApi.Ids("q", 0, 2);
        var second = FakeYouTubePagingApi.Ids("q", 2, 2);
        var api = new FakeYouTubePagingApi()
            .Page("", new FakeYouTubePage(first, "next"))
            .Page("next", new FakeYouTubePage(second, null));
        _mocker.GetMock<IYouTubeServiceWrapper>().SetupGet(wrapper => wrapper.YouTubeService).Returns(api.CreateService());
        var sut = _mocker.CreateInstance<YouTubeSearcher>();

        // Act
        var results = await sut.Search(_fixture.Create<string>(), new IndexingContext(ReleasedSince: ReleasedSince));

        // Assert
        api.Requests.Should().HaveCount(4);
        results.Select(result => result.Id).Distinct().Should().Equal(first.Concat(second));
        _logger.Entries.Should().NotContain(entry =>
            entry.Message.StartsWith(YouTubePageWalkGuard.StoppedMessagePrefix));
    }
}
