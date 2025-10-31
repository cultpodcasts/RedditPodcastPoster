using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs;

public class Episode
{
    [JsonPropertyName("synopses")]
    public required iPlayerSynopses Synopses { get; set; }
}