using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

public class CosmosSelector
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public virtual Guid Id { get; set; }

    [JsonPropertyName("type")]
    [JsonPropertyOrder(2)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public virtual ModelType ModelType { get; set; }

    [JsonPropertyName("fileKey")]
    [JsonPropertyOrder(1000)]
    public virtual string FileKey { get; set; } = "";

    [JsonPropertyName("_ts")]
    public virtual long Timestamp { get; set; }
}