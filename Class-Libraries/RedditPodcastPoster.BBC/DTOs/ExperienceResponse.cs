using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs;

public class ExperienceResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("data")]
    public required Programme[] Programmes { get; set; }
}