using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs;

public class PageProperties
{
    [JsonPropertyName("dehydratedState")]
    public required DehydratedState DehydratedState { get; set; }
}