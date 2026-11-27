using FluentAssertions;
using Indexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using Xunit;

namespace Indexer.Tests;

public class IndexingStrategyTests
{
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
        var utc = DomainTestFixture.UtcAtTime(0, TimeSpan.FromHours(hour));
        var sut = new IndexingStrategy();
        var pass = hour % 2 == 0 ? 1 : 3;

        // Act
        var result = sut.GetPassPlan(utc, pass).ResolveYouTube;

        // Assert
        result.Should().Be(expected);
    }

    [Fact(DisplayName =
        "GetPassPlan throws when the requested pass is not scheduled for the given UTC hour.")]
    public void GetPassPlan_throws_for_pass_not_running_this_hour()
    {
        // Arrange
        var utc = DomainTestFixture.UtcAtTime(0, TimeSpan.Zero);
        var sut = new IndexingStrategy();

        // Act
        var act = () => sut.GetPassPlan(utc, 3);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("pass");
    }
}
