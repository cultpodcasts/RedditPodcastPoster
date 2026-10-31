using FluentAssertions;
using RedditPodcastPoster.Discovery.ML.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Discovery.Tests.BusinessRules;

public class TrainingCsvReaderUtcRules
{
    [Fact(DisplayName =
        "When a training row's discoveryBegan has no zone or an offset, then it is read as the UTC instant, because exported discovery times are UTC and must not shift with the host's time zone.")]
    public void discovery_began_is_read_as_utc()
    {
        // Arrange
        var zoneLess = DomainTestFixture.UtcAtTime(-3, TimeSpan.FromHours(23.5));
        var withOffset = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(6));
        var path = Path.Combine(Path.GetTempPath(), $"training-{Path.GetRandomFileName()}.csv");
        File.WriteAllLines(path,
        [
            "state,discoveryBegan",
            $"Accepted,{zoneLess:yyyy-MM-ddTHH:mm:ss}",
            $"Rejected,{withOffset.AddHours(1):yyyy-MM-ddTHH:mm:ss}+01:00"
        ]);

        try
        {
            // Act
            var rows = TrainingCsvReader.ReadLabeledRows(path).ToList();

            // Assert
            rows.Select(x => x.DiscoveryBegan).Should().Equal(zoneLess, withOffset);
            rows.Should().OnlyContain(x => x.DiscoveryBegan.Kind == DateTimeKind.Utc);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
