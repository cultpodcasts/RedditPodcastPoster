// pragma: allowlist secret
using System.Text.Json.Serialization; // pragma: allowlist secret

namespace RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret

public class EpisodeServiceLink // pragma: allowlist secret
{
    [JsonPropertyName("url")]
    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? Url { get; set; }

    [JsonPropertyName("image")]
    [JsonPropertyOrder(2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? Image { get; set; }

    [JsonIgnore]
    public bool IsEmpty => Url is null && Image is null;
}
