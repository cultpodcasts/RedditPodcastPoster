using System.Text.Json.Serialization;

namespace RedditPodcastPoster.PodcastServices.Taddy.Dtos;

public class Podcast
{
    [JsonPropertyName("uuid")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
}