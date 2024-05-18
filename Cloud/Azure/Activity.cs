using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;

namespace Azure;

[CosmosSelector(ModelType.Activity)]

public sealed class Activity : CosmosSelector
{
    public Activity()
    {
        Id = Guid.NewGuid();
        ModelType = ModelType.Activity;
    }

    [JsonPropertyName("status")]
    public required string Status { get; set; }

    [JsonPropertyName("operationType")]
    public required string OperationType { get; set; }
}