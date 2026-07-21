using FluentAssertions;
using Indexer.Activities;
using Indexer.Models;
using Indexer.Orchestrations;
using Indexer.Services;
using Xunit;
using RedditPodcastPoster.Configuration.Services;

namespace Indexer.Tests;

public class IndexingStrategyTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(6, false)]
    [InlineData(12, false)]
    [InlineData(18, false)]
    public void ExpensiveYouTubeQueries_only_runs_at_midnight_utc(int hour, bool expected)
    {
        var sut = new IndexingStrategy(new FixedHourDateTimeService(hour));

        sut.ExpensiveYouTubeQueries().Should().Be(expected);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(6, true)]
    [InlineData(12, true)]
    [InlineData(18, true)]
    public void ResolveYouTube_runs_every_six_hours(int hour, bool expected)
    {
        var sut = new IndexingStrategy(new FixedHourDateTimeService(hour));

        sut.ResolveYouTube().Should().Be(expected);
    }

    private sealed class FixedHourDateTimeService(int hour) : IDateTimeService
    {
        public int GetHour() => hour;
    }
}
