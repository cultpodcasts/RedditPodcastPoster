using System.Text.Json.Serialization;

namespace RedditPodcastPoster.ContentPublisher.Models;

public class DurationResult
{
    [JsonPropertyName("duration")]
    public TimeSpan Duration { get; set; }

}