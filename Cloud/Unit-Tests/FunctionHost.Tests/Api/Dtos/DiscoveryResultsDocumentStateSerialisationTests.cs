using System.Text.Json;
using FluentAssertions;
using RedditPodcastPoster.Models.Discovery;
using Xunit;

namespace FunctionHost.Tests.Api.Dtos;

public class DiscoveryResultsDocumentStateSerialisationTests
{
    [Theory(DisplayName =
        "Discovery report state is stored by name and existing names/values are unchanged, because historic Cosmos reports and the Unprocessed query must keep working.")]
    [InlineData(DiscoveryResultsDocumentState.Unprocessed, "Unprocessed", 1)]
    [InlineData(DiscoveryResultsDocumentState.Processed, "Processed", 2)]
    [InlineData(DiscoveryResultsDocumentState.PendingSupervisor, "PendingSupervisor", 3)]
    public void state_round_trips_by_name(DiscoveryResultsDocumentState state, string name, int value)
    {
        // Arrange
        var document = new DiscoveryResultsDocument(DateTime.UtcNow, []) { SearchSince = "1d", State = state };

        // Act
        var json = JsonDocument.Parse(JsonSerializer.Serialize(document)).RootElement;
        var back = JsonSerializer.Deserialize<DiscoveryResultsDocument>(json.GetRawText())!;

        // Assert
        json.GetProperty("state").GetString().Should().Be(name);
        ((int)state).Should().Be(value);
        back.State.Should().Be(state);
    }
}
