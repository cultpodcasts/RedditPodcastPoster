using FluentAssertions;
using Xunit;
using Indexer.Activities;
using Indexer.Orchestrations;
using Indexer.Services;
using Indexer.Models;

namespace Indexer.Tests;

public class HourlyIndexingPassSelectorTests
{
    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(1, 3, 4)]
    [InlineData(2, 1, 2)]
    [InlineData(3, 3, 4)]
    [InlineData(4, 1, 2)]
    [InlineData(5, 3, 4)]
    [InlineData(6, 3, 4)]
    [InlineData(7, 1, 2)]
    [InlineData(8, 1, 2)]
    [InlineData(9, 1, 2)]
    [InlineData(10, 1, 2)]
    [InlineData(11, 1, 2)]
    [InlineData(12, 1, 2)]
    [InlineData(13, 3, 4)]
    [InlineData(14, 1, 2)]
    [InlineData(15, 3, 4)]
    [InlineData(16, 1, 2)]
    [InlineData(17, 3, 4)]
    [InlineData(18, 3, 4)]
    [InlineData(19, 1, 2)]
    [InlineData(20, 1, 2)]
    [InlineData(21, 1, 2)]
    [InlineData(22, 1, 2)]
    [InlineData(23, 1, 2)]
    public void SelectPasses_maps_hours_to_expected_batch_pairs(int hour, int expectedFirst, int expectedLast)
    {
        var (firstPass, lastPass) = HourlyIndexingPassSelector.SelectPasses(hour);

        firstPass.Should().Be(expectedFirst);
        lastPass.Should().Be(expectedLast);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(18)]
    public void SelectPasses_on_youtube_hours_covers_both_batch_halves(int hour)
    {
        var (firstPass, _) = HourlyIndexingPassSelector.SelectPasses(hour);

        if (hour % 12 == 0)
        {
            firstPass.Should().Be(1, "hours 0 and 12 should index lower batches with YouTube");
        }
        else
        {
            firstPass.Should().Be(3, "hours 6 and 18 should index upper batches with YouTube");
        }
    }

    [Fact]
    public void SelectPasses_each_batch_pair_runs_on_two_youtube_hours_per_day()
    {
        var lowerBatchYouTubeHours = Enumerable.Range(0, 24)
            .Where(h => h % 6 == 0)
            .Where(h => HourlyIndexingPassSelector.SelectPasses(h).FirstPass == 1)
            .ToArray();

        var upperBatchYouTubeHours = Enumerable.Range(0, 24)
            .Where(h => h % 6 == 0)
            .Where(h => HourlyIndexingPassSelector.SelectPasses(h).FirstPass == 3)
            .ToArray();

        lowerBatchYouTubeHours.Should().Equal(0, 12);
        upperBatchYouTubeHours.Should().Equal(6, 18);
    }
}
