using System.Text.Json.Serialization;

namespace RedditPodcastPoster.PodcastServices.Taddy.Dtos;

public class PodcastEpisode
{
    [JsonPropertyName("uuid")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("duration")]
    public int Seconds { get; set; }

    [JsonPropertyName("datePublished")]
    public long Published { get; set; }

    [JsonPropertyName("podcastSeries")]
    public Podcast? Podcast { get; set; }
}