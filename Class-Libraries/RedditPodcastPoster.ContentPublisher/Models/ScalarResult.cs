using System.Text.Json.Serialization;

namespace RedditPodcastPoster.ContentPublisher.Models;

public class ScalarResult<T>
{
    [JsonPropertyName("$1")]
    public required T Item { get; set; }
}