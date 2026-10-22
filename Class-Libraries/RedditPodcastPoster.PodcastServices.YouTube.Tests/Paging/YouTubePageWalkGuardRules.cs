using FluentAssertions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.YouTube.Paging;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Paging;

public class YouTubePageWalkGuardRules
{
    [Fact(DisplayName = "No next-page token is a normal end, even on an empty page, because the walk has finished.")]
    public void Null_next_token_is_normal_end()
    {
        var guard = new YouTubePageWalkGuard(50);

        guard.Observe("t1", [], null, 10).Should().Be(YouTubePageWalkStopReason.None);
    }

    [Fact(DisplayName = "A next-page token equal to the requested token stops the walk as RepeatedPageToken.")]
    public void Repeated_token_stops()
    {
        var guard = new YouTubePageWalkGuard(50);

        guard.Observe("t1", ["a"], "t1", null).Should().Be(YouTubePageWalkStopReason.RepeatedPageToken);
    }

    [Fact(DisplayName = "The first page (empty requested token) is never treated as a repeat.")]
    public void First_page_is_not_a_repeat()
    {
        var guard = new YouTubePageWalkGuard(50);

        guard.Observe("", ["a"], "", null).Should().Be(YouTubePageWalkStopReason.None);
        new YouTubePageWalkGuard(50).Observe("", ["a"], "t1", null).Should().Be(YouTubePageWalkStopReason.None);
    }

    [Fact(DisplayName = "A page of ids with a null id is not counted as all-seen, because the id is unknown.")]
    public void Unknown_ids_are_not_all_seen()
    {
        var guard = new YouTubePageWalkGuard(50);
        guard.Observe("", ["a"], "t1", null);

        guard.Observe("t1", ["a", null], "t2", null).Should().Be(YouTubePageWalkStopReason.None);
    }

    [Fact(DisplayName = "The page cap is ceil(totalResults / pageSize) + 2 and trips only once that many pages are fetched.")]
    public void Page_cap_is_ceiling_plus_two()
    {
        var guard = new YouTubePageWalkGuard(50);

        guard.Observe("", ["a"], "t1", 144).Should().Be(YouTubePageWalkStopReason.None);
        guard.PageCap.Should().Be(5);
        guard.Observe("t1", ["b"], "t2", 144).Should().Be(YouTubePageWalkStopReason.None);
        guard.Observe("t2", ["c"], "t3", 144).Should().Be(YouTubePageWalkStopReason.None);
        guard.Observe("t3", ["d"], "t4", 144).Should().Be(YouTubePageWalkStopReason.None);
        guard.Observe("t4", ["e"], "t5", 144).Should().Be(YouTubePageWalkStopReason.PageCapExceeded);
    }

    [Fact(DisplayName =
        "An empty page with a new token stops only once the walk has read to totalResults - 2 distinct ids, " +
        "because when clearly short it is not a loop and the cap or repeated-token check still bounds it.")]
    public void Empty_page_with_new_token_stops_only_when_read_to_end()
    {
        var shortWalk = new YouTubePageWalkGuard(50);
        shortWalk.Observe("", ["a", "b"], "t1", 10);
        shortWalk.Observe("t1", [], "t2", 10).Should().Be(YouTubePageWalkStopReason.None);

        var finished = new YouTubePageWalkGuard(50);
        finished.Observe("", ["a", "b", "c", "d", "e", "f", "g", "h"], "t1", 10);
        finished.Observe("t1", [], "t2", 10).Should().Be(YouTubePageWalkStopReason.EmptyPage);

        var noTotal = new YouTubePageWalkGuard(50);
        noTotal.Observe("", ["a"], "t1", null);
        noTotal.Observe("t1", [], "t2", null).Should().Be(YouTubePageWalkStopReason.EmptyPage);
    }

    [Fact(DisplayName =
        "A repeated token on an empty page after reading to totalResults - 2 ids logs at Warning (the Kelly " +
        "R. Minter shape is a complete read); clearly short or on a non-empty page it logs at Error.")]
    public void Repeated_token_level_depends_on_completeness()
    {
        var complete = new YouTubePageWalkGuard(50);
        complete.Observe("", FakeYouTubePagingApi.Ids("v", 0, 50).ToList<string?>(), "t2", 144);
        complete.Observe("t2", FakeYouTubePagingApi.Ids("v", 50, 50).ToList<string?>(), "t3", 144);
        complete.Observe("t3", FakeYouTubePagingApi.Ids("v", 100, 43).ToList<string?>(), "t4", 144);
        var stop = complete.Observe("t4", [], "t4", 144);
        stop.Should().Be(YouTubePageWalkStopReason.RepeatedPageToken);
        complete.LevelFor(stop).Should().Be(LogLevel.Warning);

        var clearlyShort = new YouTubePageWalkGuard(50);
        clearlyShort.Observe("", FakeYouTubePagingApi.Ids("v", 0, 50).ToList<string?>(), "t2", 300);
        stop = clearlyShort.Observe("t2", [], "t2", 300);
        stop.Should().Be(YouTubePageWalkStopReason.RepeatedPageToken);
        clearlyShort.LevelFor(stop).Should().Be(LogLevel.Error);

        var nonEmpty = new YouTubePageWalkGuard(50);
        nonEmpty.Observe("", ["a"], "t1", 2);
        stop = nonEmpty.Observe("t1", ["b"], "t1", 2);
        stop.Should().Be(YouTubePageWalkStopReason.RepeatedPageToken);
        nonEmpty.LevelFor(stop).Should().Be(LogLevel.Error);
    }

    [Fact(DisplayName = "EmptyPage logs at Information; all-seen pages and the page cap log at Error.")]
    public void Level_classification()
    {
        var guard = new YouTubePageWalkGuard(50);

        guard.LevelFor(YouTubePageWalkStopReason.None).Should().Be(LogLevel.None);
        guard.LevelFor(YouTubePageWalkStopReason.EmptyPage).Should().Be(LogLevel.Information);
        guard.LevelFor(YouTubePageWalkStopReason.NoNewItems).Should().Be(LogLevel.Error);
        guard.LevelFor(YouTubePageWalkStopReason.PageCapExceeded).Should().Be(LogLevel.Error);
    }
}
