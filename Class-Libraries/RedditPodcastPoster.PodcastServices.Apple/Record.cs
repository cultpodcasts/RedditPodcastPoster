using System.Text.Json.Serialization;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class Record
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("attributes")]
    public Attributes Attributes { get; set; } = null!;
}