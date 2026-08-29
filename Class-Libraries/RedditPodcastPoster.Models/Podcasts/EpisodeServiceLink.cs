using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Podcasts;

public class EpisodeServiceLink
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
