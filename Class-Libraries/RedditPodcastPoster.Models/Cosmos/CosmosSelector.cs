using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Cosmos;

public class CosmosSelector
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public virtual Guid Id { get; set; }

    [JsonPropertyName("type")]
    [JsonPropertyOrder(2)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public virtual ModelType ModelType { get; set; }

    [JsonPropertyName("_ts")]
    public virtual long Timestamp { get; set; }
}
