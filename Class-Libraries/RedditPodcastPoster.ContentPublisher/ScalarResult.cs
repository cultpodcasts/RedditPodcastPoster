using System.Text.Json.Serialization;

namespace RedditPodcastPoster.ContentPublisher;

public class ScalarResult<T>
{
    [JsonPropertyName("$1")]
    public required T Item { get; set; }

}