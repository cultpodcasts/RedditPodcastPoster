using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.YouTube.Tests.Paging;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.IndexingHints;

/// <summary>
/// How a playlist walk ended decides whether it may refresh the YouTube indexing hints: only a walk that read to the
/// end is Complete.
/// </summary>
public class YouTubePlaylistWalkOutcomeRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly FakeYouTubePagingApi _api = new();
    private readonly YouTubePlaylistId _playlistId;
    private readonly IYouTubeServiceWrapper _wrapper;

    public YouTubePlaylistWalkOutcomeRules()
    {
        _playlistId = new YouTubePlaylistId(_fixture.CreateYouTubePlaylistId());
        var wrapper = new AutoMocker().GetMock<IYouTubeServiceWrapper>();
        wrapper.SetupGet(x => x.YouTubeService).Returns(() => _api.CreateService());
        _wrapper = wrapper.Object;
    }

    private Task<GetPlaylistVideoSnippetsResponse> Walk(IndexingContext? context = null,
        PlaylistOrder? order = null) =>
        new YouTubePlaylistService(Mock.Of<IYouTubeQuotaUsageTracker>(),
                new RecordingLogger<YouTubePlaylistService>())
            .GetPlaylistVideoSnippets(_wrapper, _playlistId, context ?? new IndexingContext(), playlistOrder: order);

    [Fact(DisplayName =
        "A walk that ends because YouTube returns no next-page token is Complete and reports totalResults.")]
    public async Task natural_end_is_complete()
    {
        // Arrange
        _api.Page("", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 0, 50), "t2", 80))
            .Page("t2", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 50, 30), null, 80));

        // Act
        var response = await Walk();

        // Assert
        response.WalkOutcome.Should().Be(YouTubePlaylistWalkOutcome.Complete);
        response.TotalResults.Should().Be(80);
    }

    [Fact(DisplayName =
        "The repeated-token empty tail after every item was read is Complete, because the walk read the whole playlist " +
        "(the guard logs it at Warning, not Error).")]
    public async Task repeated_token_empty_tail_is_complete()
    {
        // Arrange
        _api.Page("", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 0, 50), "t2", 51))
            .Page("t2", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 50, 1), "t3", 51))
            .Page("t3", new FakeYouTubePage([], "t3", 51));

        // Act
        var response = await Walk();

        // Assert
        response.WalkOutcome.Should().Be(YouTubePlaylistWalkOutcome.Complete);
    }

    [Fact(DisplayName =
        "A walk a guard stopped at Error (a page of already-seen videos) is Incomplete, because items may be missing.")]
    public async Task guard_error_stop_is_incomplete()
    {
        // Arrange
        _api.Page("", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 0, 50), "t2", 500))
            .Page("t2", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 0, 50), "t3", 500));

        // Act
        var response = await Walk();

        // Assert
        response.WalkOutcome.Should().Be(YouTubePlaylistWalkOutcome.Incomplete);
    }

    [Fact(DisplayName =
        "A newest-first walk that stops once items leave the window is StoppedAtWindow, because it never read the " +
        "tail and needs no hints.")]
    public async Task window_stop_is_stopped_at_window()
    {
        // Arrange
        _api.Page("", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 0, 50), "t2", 500)
            {
                Newest = DomainTestFixture.UtcDaysAgo(30)
            })
            .Page("t2", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 50, 50), "t3", 500));

        // Act
        var response = await Walk(new IndexingContext(DomainTestFixture.UtcDaysAgo(2)));

        // Assert
        response.WalkOutcome.Should().Be(YouTubePlaylistWalkOutcome.StoppedAtWindow);
        _api.Requests.Should().HaveCount(1);
    }
}
