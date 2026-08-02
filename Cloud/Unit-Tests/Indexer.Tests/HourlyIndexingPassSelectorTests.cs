using FluentAssertions;
using Indexer.Orchestrations;
using Xunit;

namespace Indexer.Tests;

public class HourlyIndexingPassSelectorTests
{
    [Theory(DisplayName =
        "Each UTC hour selects a fixed pass pair: even hours run batches 1–2 and odd hours run batches 3–4.")]
    [InlineData(0, 1, 2)]
    [InlineData(1, 3, 4)]
    [InlineData(2, 1, 2)]
    [InlineData(3, 3, 4)]
    [InlineData(4, 1, 2)]
    [InlineData(5, 3, 4)]
    [InlineData(6, 1, 2)]
    [InlineData(7, 3, 4)]
    [InlineData(8, 1, 2)]
    [InlineData(9, 3, 4)]
    [InlineData(10, 1, 2)]
    [InlineData(11, 3, 4)]
    [InlineData(12, 1, 2)]
    [InlineData(13, 3, 4)]
    [InlineData(14, 1, 2)]
    [InlineData(15, 3, 4)]
    [InlineData(16, 1, 2)]
    [InlineData(17, 3, 4)]
    [InlineData(18, 1, 2)]
    [InlineData(19, 3, 4)]
    [InlineData(20, 1, 2)]
    [InlineData(21, 3, 4)]
    [InlineData(22, 1, 2)]
    [InlineData(23, 3, 4)]
    public void SelectPasses_maps_hours_to_expected_batch_pairs(int hour, int expectedFirst, int expectedLast)
    {
        // Arrange / Act
        var (firstPass, lastPass) = HourlyIndexingPassSelector.SelectPasses(hour);

        // Assert
        firstPass.Should().Be(expectedFirst);
        lastPass.Should().Be(expectedLast);
    }

    [Theory(DisplayName =
        "YouTube-enabled hours alternate batch halves so lower batches run at 0/6/12/18 UTC and upper batches at 3/9/15/21 UTC.")]
    [InlineData(0, 1)]
    [InlineData(3, 3)]
    [InlineData(6, 1)]
    [InlineData(9, 3)]
    [InlineData(12, 1)]
    [InlineData(15, 3)]
    [InlineData(18, 1)]
    [InlineData(21, 3)]
    public void SelectPasses_on_youtube_hours_covers_both_batch_halves(int hour, int expectedFirstPass)
    {
        // Arrange / Act
        var (firstPass, _) = HourlyIndexingPassSelector.SelectPasses(hour);

        // Assert
        firstPass.Should().Be(expectedFirstPass);
    }

    [Fact(DisplayName =
        "Each batch half is covered by four YouTube-enabled hours per UTC day under the every-three-hours cadence.")]
    public void SelectPasses_each_batch_pair_runs_on_four_youtube_hours_per_day()
    {
        // Arrange
        var youTubeHours = Enumerable.Range(0, 24).Where(h => h % 3 == 0);

        // Act
        var lowerBatchYouTubeHours = youTubeHours
            .Where(h => HourlyIndexingPassSelector.SelectPasses(h).FirstPass == 1)
            .ToArray();
        var upperBatchYouTubeHours = youTubeHours
            .Where(h => HourlyIndexingPassSelector.SelectPasses(h).FirstPass == 3)
            .ToArray();

        // Assert
        lowerBatchYouTubeHours.Should().Equal(0, 6, 12, 18);
        upperBatchYouTubeHours.Should().Equal(3, 9, 15, 21);
    }
}
