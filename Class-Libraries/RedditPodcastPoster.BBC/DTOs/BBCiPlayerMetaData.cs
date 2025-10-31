using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs;

public class BBCiPlayerMetaData
{
    [JsonPropertyName("episode")]
    public required Episode Episode { get; set; }
}