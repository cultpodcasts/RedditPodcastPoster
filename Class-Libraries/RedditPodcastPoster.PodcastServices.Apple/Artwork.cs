using System.Text.Json.Serialization;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class Artwork
{
    [JsonPropertyName("width")]
    public int? Width { get; set; } = null;

    [JsonPropertyName("height")]
    public int? Height { get; set; } = null;

    [JsonPropertyName("url")]
    public string? Url { get; set; } = null;
}