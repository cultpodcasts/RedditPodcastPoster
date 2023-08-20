using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

public enum ModelType
{
    [JsonPropertyName("podcast")]
    Podcast = 1,

    [JsonPropertyName("episode")]
    Episode = 2
}