using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Discovery.Models;

public class IdRecord
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
}