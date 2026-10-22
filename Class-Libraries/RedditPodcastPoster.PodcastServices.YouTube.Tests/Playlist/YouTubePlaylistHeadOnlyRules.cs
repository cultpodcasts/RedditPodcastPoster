using System.Net;
using System.Text.Json;
using System.Web;
using FluentAssertions;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Logging.Abstractions;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Playlist;

/// <summary>
/// A pass that skips expensive YouTube queries must not walk a playlist end-to-end: an unordered playlist walk was
/// what one podcast's index run spent its time on. Newest-first playlists still page while items are in the window.
/// </summary>
public class YouTubePlaylistHeadOnlyRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly PagedPlaylistHandler _pages = new();
    private readonly IndexingContext _indexingContext = new(DomainTestFixture.UtcDaysAgo(30));
    private readonly YouTubePlaylistId _playlistId;

    public YouTubePlaylistHeadOnlyRules()
    {
        _playlistId = new YouTubePlaylistId(_fixture.CreateYouTubePlaylistId());
        _mocker.Use<Microsoft.Extensions.Logging.ILogger<YouTubePlaylistService>>(
            NullLogger<YouTubePlaylistService>.Instance);
        var youTubeService = new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientFactory = new FakeHttpClientFactory(_pages),
            ApiKey = _fixture.CreateYouTubeId()
        });
        _mocker.GetMock<IYouTubeServiceWrapper>().SetupGet(x => x.YouTubeService).Returns(youTubeService);
    }

    [Fact(DisplayName =
        "When a skip-expensive pass reads a playlist whose head is not newest-first, it reads only the first page, " +
        "because walking an unordered playlist end-to-end is the expensive query deferred to the pass that allows it.")]
    public async Task unordered_playlist_reads_one_page_when_head_only()
    {
        // Arrange
        ServeAscendingPlaylist(pages: 4);
        var sut = _mocker.CreateInstance<YouTubePlaylistService>();

        // Act
        var result = await sut.GetPlaylistVideoSnippets(
            _mocker.Get<IYouTubeServiceWrapper>(), _playlistId, _indexingContext,
            firstPageOnlyUnlessNewestFirst: true);

        // Assert
        _pages.RequestCount.Should().Be(1);
        result.Result.Should().HaveCount(_pages.FirstPageSize);
        result.IsExpensiveQuery.Should().BeTrue("the head probe still reports the playlist order");
    }

    [Fact(DisplayName =
        "When the caller does not limit the read, an unordered playlist is still walked to its end, because expensive " +
        "passes and non-indexer callers keep the full walk.")]
    public async Task unordered_playlist_is_walked_fully_without_the_limit()
    {
        // Arrange
        ServeAscendingPlaylist(pages: 4);
        var sut = _mocker.CreateInstance<YouTubePlaylistService>();

        // Act
        await sut.GetPlaylistVideoSnippets(_mocker.Get<IYouTubeServiceWrapper>(), _playlistId, _indexingContext);

        // Assert
        _pages.RequestCount.Should().Be(4);
    }

    [Fact(DisplayName =
        "When a skip-expensive pass reads a newest-first playlist, it keeps paging while items are inside the window, " +
        "because a newest-first read stops at the window edge and is already cheap.")]
    public async Task newest_first_playlist_pages_while_in_window_when_head_only()
    {
        // Arrange
        _pages.Add(Item(1), Item(2), Item(3))
            .Add(Item(4))
            .Add(Item(60));
        var sut = _mocker.CreateInstance<YouTubePlaylistService>();

        // Act
        var result = await sut.GetPlaylistVideoSnippets(
            _mocker.Get<IYouTubeServiceWrapper>(), _playlistId, _indexingContext,
            firstPageOnlyUnlessNewestFirst: true);

        // Assert
        _pages.RequestCount.Should().Be(3);
        result.Result.Should().HaveCount(4, "the item outside the window is filtered out");
    }

    [Fact(DisplayName =
        "When the podcast's time budget is already cancelled, a playlist page read throws OperationCanceledException " +
        "instead of reporting an API error, so the indexer logs the budget cut-off and moves on.")]
    public async Task cancelled_budget_propagates_from_page_read()
    {
        // Arrange
        ServeAscendingPlaylist(pages: 1);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var context = _indexingContext with { CancellationToken = cancelled.Token };
        var sut = _mocker.CreateInstance<YouTubePlaylistService>();

        // Act
        var act = () => sut.GetPlaylistVideoSnippets(_mocker.Get<IYouTubeServiceWrapper>(), _playlistId, context);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private void ServeAscendingPlaylist(int pages)
    {
        // Oldest first: every item is inside the window, and each page is older than the next.
        var daysAgo = 25;
        for (var page = 0; page < pages; page++)
        {
            _pages.Add(Item(daysAgo--), Item(daysAgo--), Item(daysAgo--));
        }
    }

    private object Item(int daysAgo)
    {
        var videoId = _fixture.CreateYouTubeId();
        var addedAt = DomainTestFixture.UtcAtTime(-daysAgo, _fixture.CreateNonMidnightTimeOfDay());
        return new
        {
            snippet = new
            {
                title = _fixture.CreateTitle(),
                publishedAt = addedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                resourceId = new { videoId }
            },
            contentDetails = new { videoId, videoPublishedAt = addedAt.ToString("yyyy-MM-ddTHH:mm:ssZ") }
        };
    }

    /// <summary>Serves playlistItems.list pages in order, keyed by pageToken, and counts requests.</summary>
    private sealed class PagedPlaylistHandler : HttpMessageHandler
    {
        private readonly List<object[]> _pages = [];

        public int RequestCount { get; private set; }

        public int FirstPageSize => _pages[0].Length;

        public PagedPlaylistHandler Add(params object[] items)
        {
            _pages.Add(items);
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCount++;
            var token = HttpUtility.ParseQueryString(request.RequestUri!.Query)["pageToken"];
            var index = string.IsNullOrEmpty(token) ? 0 : int.Parse(token);
            var body = JsonSerializer.Serialize(new
            {
                items = _pages[index],
                nextPageToken = index + 1 < _pages.Count ? (index + 1).ToString() : null
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
        }
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : Google.Apis.Http.IHttpClientFactory
    {
        public Google.Apis.Http.ConfigurableHttpClient CreateHttpClient(Google.Apis.Http.CreateHttpClientArgs args) =>
            new(new Google.Apis.Http.ConfigurableMessageHandler(handler));
    }
}
