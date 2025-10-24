using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs;

public class State
{
    [JsonPropertyName("data")]
    public required ExperienceResponseWrapper ExperienceResponseWrapper { get; set; }
}