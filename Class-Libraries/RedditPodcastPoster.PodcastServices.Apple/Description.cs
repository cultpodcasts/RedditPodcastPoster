using System.Text.Json.Serialization;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class Description
{
    [JsonPropertyName("standard")]
    public string Standard { get; set; } = string.Empty;
}