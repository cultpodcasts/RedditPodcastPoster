using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs;

public class ExperienceResponseWrapper
{
    [JsonPropertyName("data")]
    public ExperienceResponse[] ExperienceResponse { get; set; }
}