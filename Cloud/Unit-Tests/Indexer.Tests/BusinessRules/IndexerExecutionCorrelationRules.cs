using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Search.Indexing;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class IndexerExecutionCorrelationRules
{
    [Fact(DisplayName =
        "Indexer correlation ignores a result whose end is after the minimum but whose start is before it, " +
        "the later start wins, and a null start is ignored, because a finished quota batch must not be counted again.")]
    public void latest_start_at_or_after_the_minimum_wins()
    {
        // Arrange
        var minimum = DomainTestFixture.UtcAtTime(0, new TimeSpan(12, 0, 0));
        var finishedEarlier = new IndexerExecutionCandidate(
            StartTime: minimum.AddMinutes(-30),
            EndTime: minimum.AddMinutes(10));
        var laterStart = new IndexerExecutionCandidate(
            StartTime: minimum.AddMinutes(5),
            EndTime: minimum.AddMinutes(6));
        var nullStart = new IndexerExecutionCandidate(
            StartTime: null,
            EndTime: minimum.AddMinutes(20));
        var candidates = new[] { finishedEarlier, nullStart, laterStart };

        // Act
        var index = IndexerExecutionCorrelation.LatestIndex(minimum, candidates);

        // Assert
        index.Should().Be(2);
        candidates[index!.Value].StartTime.Should().Be(laterStart.StartTime);
        candidates[index.Value].EndTime.Should().Be(laterStart.EndTime);
        finishedEarlier.EndTime.Should().BeAfter(minimum);
        finishedEarlier.StartTime.Should().BeBefore(minimum);
    }
}
