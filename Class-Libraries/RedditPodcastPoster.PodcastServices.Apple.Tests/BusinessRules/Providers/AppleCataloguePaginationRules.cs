using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Apple.Providers;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests.BusinessRules.Providers;

/// <summary>
/// Apple catalogue walks must treat equal release timestamps as newest-first so ReleasedSince
/// early-stop works on high-volume shows (MatchOtherServices / SubmitUrl).
/// </summary>
public class AppleCataloguePaginationRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When consecutive Apple catalogue releases share the same timestamp, IsNewestFirst is true " +
        "because equal dates are still non-increasing and must not disable ReleasedSince early-stop.")]
    public void equal_release_timestamps_count_as_newest_first()
    {
        // Arrange
        var sameRelease = DomainTestFixture.UtcAtTime(-1, _fixture.CreateNonMidnightTimeOfDay());
        var releases = new[] { sameRelease, sameRelease, sameRelease.AddHours(-2) };

        // Act
        var newestFirst = AppleCataloguePagination.IsNewestFirst(releases);

        // Assert
        newestFirst.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When an Apple catalogue page has a release ascending relative to the prior item, IsNewestFirst is false " +
        "because the head is not newest-first and ReleasedSince early-stop must not truncate the walk.")]
    public void ascending_release_counts_as_not_newest_first()
    {
        // Arrange
        var older = DomainTestFixture.UtcAtTime(-5, _fixture.CreateNonMidnightTimeOfDay());
        var newer = older.AddHours(3);
        var releases = new[] { older, newer };

        // Act
        var newestFirst = AppleCataloguePagination.IsNewestFirst(releases);

        // Assert
        newestFirst.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the first Apple page has fewer than two dated releases, IsNewestFirst is true " +
        "because a thin sample must not disable ReleasedSince early-stop on MatchOtherServices.")]
    public void thin_order_sample_counts_as_newest_first()
    {
        // Arrange
        var only = DomainTestFixture.UtcAtTime(-1, _fixture.CreateNonMidnightTimeOfDay());

        // Act
        var empty = AppleCataloguePagination.IsNewestFirst([]);
        var single = AppleCataloguePagination.IsNewestFirst([only]);

        // Assert
        empty.Should().BeTrue();
        single.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When ReleasedSince is set and the catalogue is newest-first, paging continues only while the " +
        "oldest collected release is still in-window so recent MatchOtherServices lookups stop early.")]
    public void newest_first_with_released_since_stops_once_past_window()
    {
        // Arrange
        var releasedSince = DomainTestFixture.UtcDateDaysAgo(2);
        var lastInWindow = DomainTestFixture.UtcAtTime(-1, _fixture.CreateNonMidnightTimeOfDay());
        var lastOutOfWindow = DomainTestFixture.UtcAtTime(-5, _fixture.CreateNonMidnightTimeOfDay());

        // Act
        var continueInWindow = AppleCataloguePagination.ShouldContinuePaging(
            hasNext: true,
            releasedSince,
            lastInWindow,
            newestFirst: true,
            pagesFetchedAfterFirst: 0);
        var continueOutOfWindow = AppleCataloguePagination.ShouldContinuePaging(
            hasNext: true,
            releasedSince,
            lastOutOfWindow,
            newestFirst: true,
            pagesFetchedAfterFirst: 0);

        // Assert
        continueInWindow.Should().BeTrue();
        continueOutOfWindow.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the oldest collected Apple release equals ReleasedSince on a newest-first catalogue, paging continues " +
        "because the boundary item is still in-window and the next page may hold more in-window episodes.")]
    public void newest_first_continues_when_last_collected_equals_released_since()
    {
        // Arrange
        var releasedSince = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());

        // Act
        var shouldContinue = AppleCataloguePagination.ShouldContinuePaging(
            hasNext: true,
            releasedSince,
            lastCollectedRelease: releasedSince,
            newestFirst: true,
            pagesFetchedAfterFirst: 0);

        // Assert
        shouldContinue.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When ReleasedSince is set and the catalogue is not newest-first, paging stops after MaxPages " +
        "subsequent fetches because unordered Apple walks must not pull an entire high-volume show.")]
    public void non_newest_first_with_released_since_hard_caps_subsequent_pages()
    {
        // Arrange
        var releasedSince = DomainTestFixture.UtcDateDaysAgo(2);
        var lastRelease = DomainTestFixture.UtcAtTime(-1, _fixture.CreateNonMidnightTimeOfDay());

        // Act
        var underCap = AppleCataloguePagination.ShouldContinuePaging(
            hasNext: true,
            releasedSince,
            lastRelease,
            newestFirst: false,
            pagesFetchedAfterFirst: AppleCataloguePagination.MaxPages - 1);
        var atCap = AppleCataloguePagination.ShouldContinuePaging(
            hasNext: true,
            releasedSince,
            lastRelease,
            newestFirst: false,
            pagesFetchedAfterFirst: AppleCataloguePagination.MaxPages);

        // Assert
        underCap.Should().BeTrue();
        atCap.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When ReleasedSince is null, paging continues while next links remain " +
        "because full-catalogue Apple callers still need to reach the end of the feed.")]
    public void without_released_since_continues_while_next_exists()
    {
        // Arrange
        var lastRelease = DomainTestFixture.UtcAtTime(-30, _fixture.CreateNonMidnightTimeOfDay());

        // Act
        var continueWithNext = AppleCataloguePagination.ShouldContinuePaging(
            hasNext: true,
            releasedSince: null,
            lastRelease,
            newestFirst: false,
            pagesFetchedAfterFirst: AppleCataloguePagination.MaxPages + 5);
        var stopWithoutNext = AppleCataloguePagination.ShouldContinuePaging(
            hasNext: false,
            releasedSince: null,
            lastRelease,
            newestFirst: true,
            pagesFetchedAfterFirst: 0);

        // Assert
        continueWithNext.Should().BeTrue();
        stopWithoutNext.Should().BeFalse();
    }
}
