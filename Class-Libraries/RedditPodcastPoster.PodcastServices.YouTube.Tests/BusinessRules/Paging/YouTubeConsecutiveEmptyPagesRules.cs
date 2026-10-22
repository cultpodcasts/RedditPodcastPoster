using FluentAssertions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.YouTube.Paging;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.BusinessRules.Paging;

/// <summary>
/// A page walk short of totalResults stops after two empty pages in a row (each with a new token), so an
/// over-reported total cannot stretch an empty chain to the page cap.
/// </summary>
public class YouTubeConsecutiveEmptyPagesRules
{
    private const int OverReportedTotal = 5000;
    private readonly YouTubePageWalkGuard _guard = new(50);

    public YouTubeConsecutiveEmptyPagesRules()
    {
        _guard.Observe("", ["a", "b"], "t1", OverReportedTotal);
    }

    [Fact(DisplayName =
        "Two empty pages in a row short of totalResults stop the walk as ConsecutiveEmptyPages at Error, because the " +
        "page cap alone would allow about 100 empty calls for a total of 5,000.")]
    public void two_empty_pages_in_a_row_stop_at_error()
    {
        // Arrange
        var first = _guard.Observe("t1", [], "t2", OverReportedTotal);

        // Act
        var second = _guard.Observe("t2", [], "t3", OverReportedTotal);

        // Assert
        first.Should().Be(YouTubePageWalkStopReason.None);
        second.Should().Be(YouTubePageWalkStopReason.ConsecutiveEmptyPages);
        _guard.LevelFor(second).Should().Be(LogLevel.Error);
    }

    [Fact(DisplayName =
        "One empty page followed by real items resets the count, so a single empty page mid-playlist still walks on.")]
    public void one_empty_page_then_items_walks_on()
    {
        // Arrange
        _guard.Observe("t1", [], "t2", OverReportedTotal);
        _guard.Observe("t2", ["c"], "t3", OverReportedTotal);

        // Act
        var next = _guard.Observe("t3", [], "t4", OverReportedTotal);

        // Assert
        next.Should().Be(YouTubePageWalkStopReason.None);
        _guard.ConsecutiveEmptyPages.Should().Be(1);
    }
}
