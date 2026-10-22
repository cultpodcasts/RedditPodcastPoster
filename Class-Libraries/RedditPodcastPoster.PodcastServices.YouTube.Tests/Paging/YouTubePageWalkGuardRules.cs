using FluentAssertions;
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

    [Fact(DisplayName = "EmptyPage is not an error; repeats, all-seen pages and the page cap are.")]
    public void Error_classification()
    {
        YouTubePageWalkGuard.IsError(YouTubePageWalkStopReason.EmptyPage).Should().BeFalse();
        YouTubePageWalkGuard.IsError(YouTubePageWalkStopReason.None).Should().BeFalse();
        YouTubePageWalkGuard.IsError(YouTubePageWalkStopReason.RepeatedPageToken).Should().BeTrue();
        YouTubePageWalkGuard.IsError(YouTubePageWalkStopReason.NoNewItems).Should().BeTrue();
        YouTubePageWalkGuard.IsError(YouTubePageWalkStopReason.PageCapExceeded).Should().BeTrue();
    }
}
