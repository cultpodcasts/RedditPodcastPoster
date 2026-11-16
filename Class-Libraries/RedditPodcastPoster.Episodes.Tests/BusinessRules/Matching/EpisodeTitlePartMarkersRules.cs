using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

public class EpisodeTitlePartMarkersRules
{
    [Theory(DisplayName =
        "Part and episode markers conflict only when both titles carry the same kind of marker with " +
        "different numbers, across pt/part/chapter, ep/episode, a leading or separator-bound #N, a trailing (N) up to 99, " +
        "Arabic, Roman and word numerals; years, mid-title rankings and words like Tai Chi or EPIX carry no marker.")]
    [InlineData("Story (pt.I)", "Story (pt.II)", true)]
    [InlineData("Story part 1", "Story part 2", true)]
    [InlineData("Story Part One", "Story Part Two", true)]
    [InlineData("Story Part III", "Story Part 4", true)]
    [InlineData("Story chapter 2", "Story ch. 3", true)]
    [InlineData("Show ep 3: story", "Show episode 4: story", true)]
    [InlineData("#12: story", "#13: story", true)]
    [InlineData("Story | #12", "Story | #13", true)]
    [InlineData("Story (1)", "Story (2)", true)]
    [InlineData("Story pt 1", "Story pt 10", true)]
    [InlineData("Story (pt.II)", "Story (Part 2)", false)]
    [InlineData("Story Part IV", "Story part four", false)]
    [InlineData("Show Episode 12 story", "Show ep. 12 story", false)]
    [InlineData("Story (pt.II)", "Story", false)]
    [InlineData("Show ep 3 story", "Show story part 2", false)]
    [InlineData("Deep dive into the past", "Deep dive into the archive", false)]
    [InlineData("Cult Leader Part 2 (2024)", "Cult Leader Part 2", false)]
    [InlineData("Cult Leader (2023)", "Cult Leader (2024)", false)]
    [InlineData("The #1 cult leader | Ep 12", "The No.1 cult leader Ep 12", false)]
    [InlineData("The #1 cult leader", "The #2 cult leader", false)]
    [InlineData("#MeToo and the cult ep 3", "MeToo and the cult episode 3", false)]
    [InlineData("Tai Chi cult Part 2", "Tai Chi cult pt 2", false)]
    [InlineData("Tai Chi cult", "Tai Chi cult Part 2", false)]
    [InlineData("EPIX cult documentary ep 4", "EPIX cult documentary Episode 4", false)]
    [InlineData("Now on EPIX", "Now on EPIV", false)]
    [InlineData("PTV special part 2", "PTV special pt 2", false)]
    [InlineData("Story (2)", "Story Part 3", false)]
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
