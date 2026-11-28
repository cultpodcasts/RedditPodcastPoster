using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Operations;

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
    /// Cosmos item TTL (seconds). Written by the Activity <c>bookActivity</c> sproc
    /// (see Cloud <c>ActivityBookingTtl</c>).
    /// </summary>
    [JsonPropertyName("ttl")]
    public int? TimeToLiveSeconds { get; set; }
}
