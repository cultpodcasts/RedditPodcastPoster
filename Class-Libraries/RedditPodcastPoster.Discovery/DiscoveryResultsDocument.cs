using RedditPodcastPoster.Models;
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Discovery;

[CosmosSelector(ModelType.Discovery)]
public sealed class DiscoveryResultsDocument : CosmosSelector
{
    public DiscoveryResultsDocument(DateTime discoveryBegan, IEnumerable<DiscoveryResult> discoveryResults)
    {
        Id = Guid.NewGuid();
        ModelType = ModelType.Discovery;
        DiscoveryBegan = discoveryBegan;
        DiscoveryResults = discoveryResults;
        State = DiscoveryResultState.Unprocessed;
    }

    [JsonPropertyName("discoveryBegan")]
    public DateTime DiscoveryBegan { get; set; }

    [JsonPropertyName("discoveryResults")]
    public IEnumerable<DiscoveryResult> DiscoveryResults { get; set; }

    [JsonPropertyName("state")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DiscoveryResultState State { get; set; }
}

public enum DiscoveryResultState
{
    None = 0,
    Unprocessed=1,
    Processed=2
}