using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs;

public class Episode
{
    [JsonPropertyName("synopses")]
    public required iPlayerSynopses Synopses { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; set; }
}