using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.YouTube.Tests.Paging;
using YouTubeChannel = Google.Apis.YouTube.v3.Data.Channel;
using YouTubeVideo = Google.Apis.YouTube.v3.Data.Video;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Serialization;
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
        "keeps the videos already found, and logs Warning for each duration, because each repeated call costs 100 quota units.")]
    public async Task repeating_empty_page_ends_the_search_and_keeps_earlier_results()
    {
        // Arrange
        var found = FakeYouTubePagingApi.Ids("q", 0, 3);
        var api = new FakeYouTubePagingApi()
            .Page("", Videos(found, "repeat"))
            .Page("repeat", new FakeYouTubePage([], "repeat"));
        _mocker.GetMock<IYouTubeServiceWrapper>().SetupGet(wrapper => wrapper.YouTubeService).Returns(api.CreateService());
        var sut = _mocker.CreateInstance<YouTubeSearcher>();

        // Act
        var results = await sut.Search(_fixture.Create<string>(), SearchWindow());

        // Assert
        api.Requests.Should().HaveCount(4);
        results.Select(result => result.Id).Distinct().Should().Equal(found);
        var stops = StopEntries();
        stops.Should().HaveCount(2);
        stops.Should().OnlyContain(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains(nameof(YouTubePageWalkStopReason.RepeatedPageToken)));
    }

    [Fact(DisplayName =
        "A query search stops at Warning when a later page repeats ids already seen and still offers another page, " +
        "and keeps those earlier ids, because search.list repeats results and another page costs 100 quota units.")]
    public async Task already_seen_page_stops_at_warning_before_the_next_page()
    {
        // Arrange
        var seen = FakeYouTubePagingApi.Ids("q", 0, 3);
        var later = FakeYouTubePagingApi.Ids("q", 3, 2);
        var api = new FakeYouTubePagingApi()
            .Page("", Videos(seen, "next"))
            .Page("next", Videos(seen, "further"))
            .Page("further", Videos(later, null));
        _mocker.GetMock<IYouTubeServiceWrapper>().SetupGet(wrapper => wrapper.YouTubeService).Returns(api.CreateService());
        var sut = _mocker.CreateInstance<YouTubeSearcher>();

        // Act
        var results = await sut.Search(_fixture.Create<string>(), SearchWindow());

        // Assert
        api.Requests.Should().HaveCount(4);
        results.Select(result => result.Id).Distinct().Should().Equal(seen);
        results.Select(result => result.Id).Should().NotContain(later);
        var stops = StopEntries();
        stops.Should().HaveCount(2);
        stops.Should().OnlyContain(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains(nameof(YouTubePageWalkStopReason.NoNewItems)));
    }

    [Fact(DisplayName =
        "A query search ignores an estimated pageInfo.totalResults, so an empty page with a new token stops as " +
        "Information EmptyPage, does not fetch the following page, and keeps the videos already found.")]
    public async Task estimated_total_does_not_keep_an_empty_page_walking()
    {
        // Arrange
        var found = FakeYouTubePagingApi.Ids("q", 0, 3);
        var later = FakeYouTubePagingApi.Ids("q", 3, 2);
        const int estimatedTotal = 5_000;
        var api = new FakeYouTubePagingApi()
            .Page("", Videos(found, "next"))
            .Page("next", new FakeYouTubePage([], "further", estimatedTotal))
            .Page("further", Videos(later, null));
        _mocker.GetMock<IYouTubeServiceWrapper>().SetupGet(wrapper => wrapper.YouTubeService).Returns(api.CreateService());
        var sut = _mocker.CreateInstance<YouTubeSearcher>();

        // Act
        var results = await sut.Search(_fixture.Create<string>(), SearchWindow());

        // Assert
        api.Requests.Should().HaveCount(4);
        results.Select(result => result.Id).Distinct().Should().Equal(found);
        results.Select(result => result.Id).Should().NotContain(later);
        var stops = StopEntries();
        stops.Should().HaveCount(2);
        stops.Should().OnlyContain(entry =>
            entry.Level == LogLevel.Information &&
            entry.Message.Contains(nameof(YouTubePageWalkStopReason.EmptyPage)));
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
            .Page("", Videos(first, "next"))
            .Page("next", Videos(second, null));
        _mocker.GetMock<IYouTubeServiceWrapper>().SetupGet(wrapper => wrapper.YouTubeService).Returns(api.CreateService());
        var sut = _mocker.CreateInstance<YouTubeSearcher>();

        // Act
        var results = await sut.Search(_fixture.Create<string>(), SearchWindow());

        // Assert
        api.Requests.Should().HaveCount(4);
        results.Select(result => result.Id).Distinct().Should().Equal(first.Concat(second));
        _logger.Entries.Should().NotContain(entry =>
            entry.Message.StartsWith(YouTubePageWalkGuard.StoppedMessagePrefix));
    }

    private static IndexingContext SearchWindow() =>
        new(ReleasedSince: DomainTestFixture.UtcDaysAgo(7));

    private static FakeYouTubePage Videos(IReadOnlyList<string> videoIds, string? nextPageToken) =>
        new(videoIds, nextPageToken)
        {
            Newest = UtcDateTimes.ToUtcOffset(DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(12)))
        };

    private List<(LogLevel Level, string Message)> StopEntries() =>
        _logger.Entries.Where(entry => entry.Message.StartsWith(YouTubePageWalkGuard.StoppedMessagePrefix)).ToList();
}
