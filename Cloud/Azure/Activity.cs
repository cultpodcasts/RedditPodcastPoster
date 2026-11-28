using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

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

    /// <summary>
    /// Cosmos item TTL (seconds). Set by <c>bookActivity</c> — see <see cref="ActivityBookingTtl"/>.
    /// </summary>
    [JsonPropertyName("ttl")]
    public int? TimeToLiveSeconds { get; set; }
}