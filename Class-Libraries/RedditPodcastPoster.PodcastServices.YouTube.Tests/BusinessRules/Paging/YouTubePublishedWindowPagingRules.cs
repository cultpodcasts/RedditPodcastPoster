using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.YouTube.Tests.Paging;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.Paging;

/// <summary>
/// Newest-first playlist paging stops on added-at, while the indexing window uses the later of added-at and
/// video-published-at. After added-at leaves the window the walk reads a bounded number of further pages while the
/// page just read still has an item inside the window.
/// </summary>
public class YouTubePublishedWindowPagingRules
{
    private const int PageSize = YouTubePlaylistService.PlaylistPageSize;

    private readonly DomainTestFixture _fixture = new();
    private readonly FakeYouTubePagingApi _api = new();
    private readonly YouTubePlaylistId _playlistId;
    private readonly DateTime _windowStart = DomainTestFixture.UtcDaysAgo(2);
    private readonly DateTime _inside;
    private readonly DateTime _addedOutside = DomainTestFixture.UtcDaysAgo(40);
    private readonly string _targetVideoId;
    private readonly string _beyondBoundVideoId;

    public YouTubePublishedWindowPagingRules()
    {
        _inside = DomainTestFixture.UtcAtTime(0, _fixture.CreateNonMidnightTimeOfDay());
        _playlistId = new YouTubePlaylistId(
            _fixture.CreateYouTubePlaylistId(),
            YouTubePlaylistIdSource.PodcastEntity,
            _fixture.CreateGuid().ToString());
        _targetVideoId = _fixture.CreateYouTubeId();
        _beyondBoundVideoId = _fixture.CreateYouTubeId();

        // Four pages, newest-first by added-at (equal added-at counts as newest-first). Each of the first three
        // pages has one video published inside the window, so the extra-page allowance is used in full. The fourth
        // page is past the bound.
        var fetchedPages = 1 + YouTubePlaylistService.PublishedWindowExtraPages;
        for (var page = 0; page < fetchedPages + 1; page++)
        {
            var token = page == 0 ? "" : $"t{page + 1}";
            var next = $"t{page + 2}";
            var inWindowId = page == 1
                ? _targetVideoId
                : page == fetchedPages
                    ? _beyondBoundVideoId
                    : _fixture.CreateYouTubeId();
            _api.Page(token, PageOf(inWindowId, next, total: PageSize * (fetchedPages + 1)));
        }
    }

    [Fact(DisplayName =
        "A video added outside the indexing window but published inside it, on the second page of a newest-first " +
        "playlist, is still returned, because paging continues while the page just read has an item inside the " +
        "window (added-at alone would have stopped before that page).")]
    public async Task published_inside_window_on_later_page_is_returned()
    {
        // Arrange
        var context = new IndexingContext(_windowStart);

        // Act
        var response = await CreateSut().GetPlaylistVideoSnippets(Wrapper(), _playlistId, context);

        // Assert
        response.Failure.Should().BeNull();
        response.IsExpensiveQuery.Should().BeFalse();
        var item = response.Result.Should().ContainSingle(x => x.GetVideoId() == _targetVideoId).Subject;
        item.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime.Should().BeBefore(_windowStart);
        item.ContentDetails.VideoPublishedAtDateTimeOffset!.Value.UtcDateTime.Should().BeOnOrAfter(_windowStart);
        response.Result.Should().NotContain(x => x.GetVideoId() == _beyondBoundVideoId);
    }

    [Fact(DisplayName =
        "After added-at leaves the window, a newest-first walk reads at most the published-window extra-page bound " +
        "and does not fetch the next page, because each extra playlistItems.list page costs one quota unit.")]
    public async Task extra_pages_stop_at_the_quota_bound()
    {
        // Arrange
        var context = new IndexingContext(_windowStart);

        // Act
        var response = await CreateSut().GetPlaylistVideoSnippets(Wrapper(), _playlistId, context);

        // Assert
        _api.Requests.Should().HaveCount(1 + YouTubePlaylistService.PublishedWindowExtraPages);
        response.WalkOutcome.Should().Be(YouTubePlaylistWalkOutcome.StoppedAtWindow);
        response.Result.Should().NotContain(x => x.GetVideoId() == _beyondBoundVideoId);
        response.Result.Select(x => x.GetVideoId()).Should().Contain(_targetVideoId);
    }

    private YouTubePlaylistService CreateSut() =>
        new(Mock.Of<IYouTubeQuotaUsageTracker>(), new RecordingLogger<YouTubePlaylistService>());

    private IYouTubeServiceWrapper Wrapper()
    {
        var wrapper = new AutoMocker().GetMock<IYouTubeServiceWrapper>();
        wrapper.SetupGet(x => x.YouTubeService).Returns(_api.CreateService());
        return wrapper.Object;
    }

    private FakeYouTubePage PageOf(string inWindowVideoId, string nextToken, int total)
    {
        var ids = Enumerable.Range(0, PageSize).Select(_ => _fixture.CreateYouTubeId()).ToList();
        ids[PageSize / 2] = inWindowVideoId;
        var added = Enumerable.Repeat((DateTimeOffset?)new DateTimeOffset(_addedOutside), PageSize).ToList();
        var published = Enumerable.Repeat((DateTimeOffset?)new DateTimeOffset(_addedOutside), PageSize).ToList();
        published[PageSize / 2] = new DateTimeOffset(_inside);
        return new FakeYouTubePage(ids, nextToken, total)
        {
            Dates = added,
            VideoPublishedAt = published
        };
    }
}
