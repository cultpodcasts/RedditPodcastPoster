using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Paging;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Paging;

/// <summary>
/// playlistItems.list walks page at the YouTube maximum and stop on YouTube paging faults (a next-page
/// token that points back at itself, an empty page, a page of already-seen videos, or more pages than
/// totalResults allows) while keeping the items gathered so far, so indexing still proceeds.
/// </summary>
public class YouTubePlaylistServicePagingRules
{
    private const string PlaylistIdValue = "PLsyntheticplaylist0001";
    private const string RepeatingToken = "synthetic-repeating-token";

    private readonly FakeYouTubePagingApi _api = new();
    private readonly RecordingLogger<YouTubePlaylistService> _logger = new();

    private static readonly YouTubePlaylistId PlaylistId =
        new(PlaylistIdValue, YouTubePlaylistIdSource.PodcastEntity, "00000000-0000-0000-0000-00000000a001");

    private YouTubePlaylistService CreateSut() =>
        new(Mock.Of<IYouTubeQuotaUsageTracker>(), _logger);

    private IYouTubeServiceWrapper Wrapper()
    {
        var wrapper = new AutoMocker().GetMock<IYouTubeServiceWrapper>();
        wrapper.SetupGet(x => x.YouTubeService).Returns(_api.CreateService());
        return wrapper.Object;
    }

    private IEnumerable<(LogLevel Level, string Message)> StopLogs =>
        _logger.Entries.Where(e => e.Message.StartsWith(YouTubePageWalkGuard.StoppedMessagePrefix));

    [Fact(DisplayName =
        "A playlist whose last page hands back a next-page token for an empty page that repeats the same " +
        "token stops after one empty fetch, keeps every item and logs Warning, because following the token loops " +
        "forever but the read is complete (the Kelly R. Minter pattern: 143 of 144 items, about 347 looping calls " +
        "at 10 a page before this fix).")]
    public async Task Repeating_token_empty_page_terminates()
    {
        // Arrange
        _api.Page("", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 0, 50), "t2", 144))
            .Page("t2", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 50, 50), "t3", 144))
            .Page("t3", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 100, 43), RepeatingToken, 144))
            .Page(RepeatingToken, new FakeYouTubePage([], RepeatingToken, 144));

        // Act
        var response = await CreateSut().GetPlaylistVideoSnippets(Wrapper(), PlaylistId, new IndexingContext());

        // Assert
        _api.Requests.Should().HaveCount(4);
        response.Failure.Should().BeNull();
        response.Result.Should().HaveCount(143);
        var stop = StopLogs.Should().ContainSingle().Subject;
        stop.Level.Should().Be(LogLevel.Warning);
        stop.Message.Should().Contain(nameof(YouTubePageWalkStopReason.RepeatedPageToken))
            .And.Contain(PlaylistIdValue)
            .And.Contain("00000000-0000-0000-0000-00000000a001")
            .And.Contain("pages-fetched='4'")
            .And.Contain("total-results='144'")
            .And.Contain("page-size='50'")
            .And.NotContain(RepeatingToken);
    }

    [Fact(DisplayName =
        "A page with zero items that still carries a new next-page token ends the walk at Information, " +
        "because there is nothing left to read and the token cannot be trusted.")]
    public async Task Empty_page_with_new_token_terminates()
    {
        // Arrange
        _api.Page("", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 0, 12), "t2", 12))
            .Page("t2", new FakeYouTubePage([], "t3", 12))
            .Page("t3", new FakeYouTubePage([], "t4", 12));

        // Act
        var response = await CreateSut().GetPlaylistVideoSnippets(Wrapper(), PlaylistId, new IndexingContext());

        // Assert
        _api.Requests.Should().HaveCount(2);
        response.Result.Should().HaveCount(12);
        var stop = StopLogs.Should().ContainSingle().Subject;
        stop.Level.Should().Be(LogLevel.Information);
        stop.Message.Should().Contain(nameof(YouTubePageWalkStopReason.EmptyPage));
    }

    [Fact(DisplayName =
        "A repeated token on an empty page well short of totalResults stops the walk at Error, because items " +
        "YouTube says exist were never returned.")]
    public async Task Repeating_token_clearly_short_logs_error()
    {
        // Arrange
        _api.Page("", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 0, 50), "t2", 300))
            .Page("t2", new FakeYouTubePage([], "t2", 300));

        // Act
        var response = await CreateSut().GetPlaylistVideoSnippets(Wrapper(), PlaylistId, new IndexingContext());

        // Assert
        _api.Requests.Should().HaveCount(2);
        response.Result.Should().HaveCount(50);
        var stop = StopLogs.Should().ContainSingle().Subject;
        stop.Level.Should().Be(LogLevel.Error);
        stop.Message.Should().Contain(nameof(YouTubePageWalkStopReason.RepeatedPageToken));
    }

    [Fact(DisplayName =
        "An empty page with a new token well short of totalResults does not stop the walk, so a later page's " +
        "items are still read, because only a loop or the page cap should end a walk that is clearly incomplete.")]
    public async Task Empty_page_with_new_token_short_of_total_continues()
    {
        // Arrange
        _api.Page("", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 0, 50), "t2", 100))
            .Page("t2", new FakeYouTubePage([], "t3", 100))
            .Page("t3", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 50, 50), null, 100));

        // Act
        var response = await CreateSut().GetPlaylistVideoSnippets(Wrapper(), PlaylistId, new IndexingContext());

        // Assert
        _api.Requests.Select(r => r.PageToken).Should().Equal("", "t2", "t3");
        response.Result.Should().HaveCount(100);
        StopLogs.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A chain of empty pages with new tokens short of totalResults stops after two empty pages in a row at Error, " +
        "because an over-reported total (5,000 here) would otherwise let the page cap allow about 100 empty calls.")]
    public async Task Empty_pages_short_of_total_stop_after_two_in_a_row()
    {
        // Arrange: totalResults 5,000 => page cap ceil(5000/50) + 2 = 102 pages.
        _api.Page("", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 0, 10), "t1", 5000));
        for (var page = 1; page < 10; page++)
        {
            _api.Page($"t{page}", new FakeYouTubePage([], $"t{page + 1}", 5000));
        }

        // Act
        var response = await CreateSut().GetPlaylistVideoSnippets(Wrapper(), PlaylistId, new IndexingContext());

        // Assert
        _api.Requests.Select(r => r.PageToken).Should().Equal("", "t1", "t2");
        response.Result.Should().HaveCount(10);
        response.WalkOutcome.Should().Be(YouTubePlaylistWalkOutcome.Incomplete);
        var stop = StopLogs.Should().ContainSingle().Subject;
        stop.Level.Should().Be(LogLevel.Error);
        stop.Message.Should().Contain(nameof(YouTubePageWalkStopReason.ConsecutiveEmptyPages));
    }

    [Fact(DisplayName =
        "A page made only of videos already seen in this walk stops the walk at Error and keeps the items, " +
        "because YouTube is replaying pages and every further call is wasted quota.")]
    public async Task All_seen_ids_terminate()
    {
        // Arrange
        var ids = FakeYouTubePagingApi.Ids("v", 0, 50);
        _api.Page("", new FakeYouTubePage(ids, "t2"))
            .Page("t2", new FakeYouTubePage(ids, "t3"))
            .Page("t3", new FakeYouTubePage(ids, "t4"));

        // Act
        var response = await CreateSut().GetPlaylistVideoSnippets(Wrapper(), PlaylistId, new IndexingContext());

        // Assert
        _api.Requests.Should().HaveCount(2);
        response.Result.Should().NotBeNullOrEmpty();
        response.Result!.Select(x => x.Snippet.ResourceId.VideoId).Distinct().Should().HaveCount(50);
        var stop = StopLogs.Should().ContainSingle().Subject;
        stop.Level.Should().Be(LogLevel.Error);
        stop.Message.Should().Contain(nameof(YouTubePageWalkStopReason.NoNewItems));
    }

    [Fact(DisplayName =
        "A walk that keeps getting fresh pages stops at ceil(totalResults / 50) + 2 pages and logs Error, " +
        "because YouTube said the playlist is smaller than what it is serving.")]
    public async Task Page_cap_terminates()
    {
        // Arrange: totalResults 60 => cap ceil(60/50) + 2 = 4 pages.
        for (var page = 0; page < 10; page++)
        {
            var token = page == 0 ? "" : $"t{page}";
            _api.Page(token, new FakeYouTubePage(FakeYouTubePagingApi.Ids($"p{page}-", 0, 50), $"t{page + 1}", 60));
        }

        // Act
        var response = await CreateSut().GetPlaylistVideoSnippets(Wrapper(), PlaylistId, new IndexingContext());

        // Assert
        _api.Requests.Should().HaveCount(4);
        response.Result.Should().HaveCount(200);
        var stop = StopLogs.Should().ContainSingle().Subject;
        stop.Level.Should().Be(LogLevel.Error);
        stop.Message.Should().Contain(nameof(YouTubePageWalkStopReason.PageCapExceeded))
            .And.Contain("pages-fetched='4'")
            .And.Contain("total-results='60'");
    }

    [Fact(DisplayName =
        "A normal multi-page walk follows each token until YouTube returns none and returns every item " +
        "with no stop log, because the guards must not cut a healthy walk short.")]
    public async Task Normal_multi_page_walk_returns_every_item()
    {
        // Arrange
        _api.Page("", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 0, 50), "t2", 143))
            .Page("t2", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 50, 50), "t3", 143))
            .Page("t3", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 100, 43), null, 143));

        // Act
        var response = await CreateSut().GetPlaylistVideoSnippets(Wrapper(), PlaylistId, new IndexingContext());

        // Assert
        _api.Requests.Select(r => r.PageToken).Should().Equal("", "t2", "t3");
        response.Result!.Select(x => x.Snippet.ResourceId.VideoId).Should()
            .Equal(FakeYouTubePagingApi.Ids("v", 0, 143));
        StopLogs.Should().BeEmpty();
        _logger.Entries.Should().NotContain(e => e.Level >= LogLevel.Warning);
    }

    [Theory(DisplayName =
        "Every playlistItems.list request asks for 50 items (the YouTube maximum) whatever the expensive flag, " +
        "date window or declared order, because the call costs 1 unit per page regardless of size.")]
    [InlineData(false, false, null)]
    [InlineData(true, false, null)]
    [InlineData(false, true, null)]
    [InlineData(true, true, null)]
    [InlineData(false, true, PlaylistOrder.Arbitrary)]
    [InlineData(true, false, PlaylistOrder.Arbitrary)]
    public async Task Page_size_fifty_is_used(bool expensivePlaylist, bool withWindow, PlaylistOrder? order)
    {
        // Arrange: every item is inside the window, so a dated walk reads all three pages.
        var newest = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
        _api.Page("", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 0, 50), "t2", 120) { Newest = newest })
            .Page("t2", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 50, 50), "t3", 120) { Newest = newest })
            .Page("t3", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 100, 20), null, 120) { Newest = newest });
        var context = new IndexingContext(withWindow ? newest.UtcDateTime.AddDays(-30) : null);

        // Act
        await CreateSut().GetPlaylistVideoSnippets(Wrapper(), PlaylistId, context,
            expensivePlaylist: expensivePlaylist, playlistOrder: order);

        // Assert
        _api.Requests.Should().NotBeEmpty();
        _api.Requests.Should().OnlyContain(r => r.MaxResults == YouTubePlaylistService.PlaylistPageSize);
        YouTubePlaylistService.PlaylistPageSize.Should().Be(50);
    }

    [Fact(DisplayName =
        "A newest-first playlist with a date window still stops after the first page once items leave the " +
        "window, because 50-item pages must keep the cheap early stop for channel uploads.")]
    public async Task Newest_first_window_still_stops_early()
    {
        // Arrange: items one minute apart, newest first; window covers only the first few.
        var newest = new DateTimeOffset(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
        _api.Page("", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 0, 50), "t2", 500) { Newest = newest })
            .Page("t2", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 50, 50), "t3", 500) { Newest = newest.AddMinutes(-50) });
        var context = new IndexingContext(newest.UtcDateTime.AddMinutes(-5));

        // Act
        var response = await CreateSut().GetPlaylistVideoSnippets(Wrapper(), PlaylistId, context);

        // Assert
        _api.Requests.Should().HaveCount(1);
        response.IsExpensiveQuery.Should().BeFalse();
        response.Result.Should().NotBeEmpty();
    }

    private static readonly DateTimeOffset ProbeNow = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 50 dated items, newest-first one hour apart, except where <paramref name="overrides"/> says.
    /// Items 1-10 sit inside a 10.5-hour window; the last item is well outside it.
    /// </summary>
    private static IReadOnlyList<DateTimeOffset?> ProbeDates(params (int Position, DateTimeOffset? Date)[] overrides)
    {
        var dates = Enumerable.Range(0, 50).Select(i => (DateTimeOffset?) ProbeNow.AddHours(-i)).ToList();
        foreach (var (position, date) in overrides)
        {
            dates[position - 1] = date;
        }

        return dates;
    }

    private static IndexingContext ProbeWindow() => new(ProbeNow.UtcDateTime.AddHours(-10.5));

    [Fact(DisplayName =
        "The newest-first check looks only at the first 3 items: items 1-3 newest-first with item 4 out of " +
        "order is still newest-first, so the walk stops after page 1 and the playlist is not flagged expensive " +
        "(the old 3-item first page saw exactly this, so 50-item pages must not reclassify it).")]
    public async Task Order_check_ignores_items_after_the_third()
    {
        // Arrange: item 4 is newer than item 3, breaking the order after the probe sample.
        var dates = ProbeDates((4, ProbeNow.AddMinutes(-30)));
        _api.Page("", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 0, 50), "t2", 100) { Dates = dates })
            .Page("t2", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 50, 50), null, 100) { Dates = dates });

        // Act
        var response = await CreateSut().GetPlaylistVideoSnippets(Wrapper(), PlaylistId, ProbeWindow());

        // Assert
        response.IsExpensiveQuery.Should().BeFalse();
        _api.Requests.Should().HaveCount(1);
    }

    [Fact(DisplayName =
        "Items 1-3 not newest-first classify the playlist as expensive and walk it to the end, because the " +
        "early stop is only safe for newest-first playlists.")]
    public async Task Order_check_out_of_order_head_walks_to_end()
    {
        // Arrange: item 2 is newer than item 1.
        var dates = ProbeDates((2, ProbeNow.AddMinutes(30)));
        _api.Page("", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 0, 50), "t2", 100) { Dates = dates })
            .Page("t2", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 50, 50), null, 100) { Dates = dates });

        // Act
        var response = await CreateSut().GetPlaylistVideoSnippets(Wrapper(), PlaylistId, ProbeWindow());

        // Assert
        response.IsExpensiveQuery.Should().BeTrue();
        _api.Requests.Select(r => r.PageToken).Should().Equal("", "t2");
    }

    [Fact(DisplayName =
        "The newest-first check takes the first 3 items and then drops undated ones: with item 1 undated it " +
        "judges items 2-3 only, so an out-of-order item 4 does not count (matching the old 3-item first page).")]
    public async Task Order_check_takes_three_then_filters_undated()
    {
        // Arrange: item 1 has no date; items 2-3 newest-first; item 4 newer than item 3.
        var dates = ProbeDates((1, null), (4, ProbeNow.AddMinutes(-30)));
        _api.Page("", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 0, 50), "t2", 100) { Dates = dates })
            .Page("t2", new FakeYouTubePage(FakeYouTubePagingApi.Ids("v", 50, 50), null, 100) { Dates = dates });

        // Act
        var response = await CreateSut().GetPlaylistVideoSnippets(Wrapper(), PlaylistId, ProbeWindow());

        // Assert
        response.IsExpensiveQuery.Should().BeFalse();
        _api.Requests.Should().HaveCount(1);
    }
}
