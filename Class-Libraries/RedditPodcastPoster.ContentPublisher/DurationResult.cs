using System.Text.Json.Serialization;

namespace RedditPodcastPoster.ContentPublisher;

public class DurationResult
{
    [JsonPropertyName("duration")]
    public TimeSpan Duration { get; set; }

}