using FluentAssertions;
using RedditPodcastPoster.UrlSubmission.Matching;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules;

public class EpisodeTitlePartMarkersRules
{
    [Theory(DisplayName =
        "Part and episode markers conflict only when both titles carry the same kind of marker with " +
        "different numbers, across pt/part/chapter, ep/episode/#, a trailing (N), Arabic, Roman and word numerals.")]
    [InlineData("Story (pt.I)", "Story (pt.II)", true)]
    [InlineData("Story part 1", "Story part 2", true)]
    [InlineData("Story Part One", "Story Part Two", true)]
    [InlineData("Story Part III", "Story Part 4", true)]
    [InlineData("Story chapter 2", "Story ch. 3", true)]
    [InlineData("Show ep 3: story", "Show episode 4: story", true)]
    [InlineData("Show #12 story", "Show #13 story", true)]
    [InlineData("Story (1)", "Story (2)", true)]
    [InlineData("Story pt 1", "Story pt 10", true)]
    [InlineData("Story (pt.II)", "Story (Part 2)", false)]
    [InlineData("Story Part IV", "Story part four", false)]
    [InlineData("Show Episode 12 story", "Show ep. 12 story", false)]
    [InlineData("Story (pt.II)", "Story", false)]
    [InlineData("Show ep 3 story", "Show story part 2", false)]
    [InlineData("Deep dive into the past", "Deep dive into the archive", false)]
    public void part_markers_conflict_only_when_both_sides_differ(string left, string right, bool expected)
    {
        // Arrange
        var forward = left;
        var reverse = right;

        // Act
        var conflict = EpisodeTitlePartMarkers.Conflict(forward, reverse);
        var conflictReversed = EpisodeTitlePartMarkers.Conflict(reverse, forward);

        // Assert
        conflict.Should().Be(expected);
        conflictReversed.Should().Be(expected);
    }
}
