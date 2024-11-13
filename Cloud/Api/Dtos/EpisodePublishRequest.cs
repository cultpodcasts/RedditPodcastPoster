using System.Text.Json.Serialization;

namespace Api.Dtos;

public class EpisodePublishRequest
{
    [JsonPropertyName("post")]
    public bool Post { get; set; }

    [JsonPropertyName("tweet")]
    public bool Tweet { get; set; }

    [JsonPropertyName("blueskyPost")]
    public bool BlueskyPost { get; set; }
}