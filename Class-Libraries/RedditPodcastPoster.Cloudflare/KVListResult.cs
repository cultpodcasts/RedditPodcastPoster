using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Cloudflare;

public class KVListResult
{
    [JsonPropertyName("expiration")]
    public long Expiration { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("metadata")]
    public JsonNode MetaData { get; set; }

}
