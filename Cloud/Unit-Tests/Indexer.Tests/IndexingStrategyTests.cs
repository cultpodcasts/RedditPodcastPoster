using FluentAssertions;
using Indexer.Services;
using RedditPodcastPoster.Configuration.Services;
using Xunit;

namespace Indexer.Tests;

public class IndexingStrategyTests
{
    [Theory(DisplayName =
        "Expensive YouTube queries run only at midnight UTC because unbounded playlist walks must stay on one primary pass per day.")]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    [InlineData(5, false)]
    [InlineData(6, false)]
    [InlineData(7, false)]
    [InlineData(8, false)]
    [InlineData(9, false)]
    [InlineData(10, false)]
    [InlineData(11, false)]
    [InlineData(12, false)]
    [InlineData(13, false)]
    [InlineData(14, false)]
    [InlineData(15, false)]
    [InlineData(16, false)]
    [InlineData(17, false)]
    [InlineData(18, false)]
    [InlineData(19, false)]
    [InlineData(20, false)]
    [InlineData(21, false)]
    [InlineData(22, false)]
    [InlineData(23, false)]
    public void ExpensiveYouTubeQueries_only_runs_at_midnight_utc(int hour, bool expected)
    {
        // Arrange
        var sut = new IndexingStrategy(new FixedHourDateTimeService(hour));

        // Act
        var result = sut.ExpensiveYouTubeQueries();

        // Assert
        result.Should().Be(expected);
    }

    [Theory(DisplayName =
        "YouTube URL resolving runs every three UTC hours so YouTube-authority podcasts are discovered four times per batch half per day.")]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(5, false)]
    [InlineData(6, true)]
    [InlineData(7, false)]
    [InlineData(8, false)]
    [InlineData(9, true)]
    [InlineData(10, false)]
    [InlineData(11, false)]
    [InlineData(12, true)]
    [InlineData(13, false)]
    [InlineData(14, false)]
    [InlineData(15, true)]
    [InlineData(16, false)]
    [InlineData(17, false)]
    [InlineData(18, true)]
    [InlineData(19, false)]
    [InlineData(20, false)]
    [InlineData(21, true)]
    [InlineData(22, false)]
    [InlineData(23, false)]
    public void ResolveYouTube_runs_every_three_hours(int hour, bool expected)
    {
        // Arrange
        var sut = new IndexingStrategy(new FixedHourDateTimeService(hour));

        // Act
        var result = sut.ResolveYouTube();

        // Assert
        result.Should().Be(expected);
    }

    private sealed class FixedHourDateTimeService(int hour) : IDateTimeService
    {
        public int GetHour() => hour;
    }
}
