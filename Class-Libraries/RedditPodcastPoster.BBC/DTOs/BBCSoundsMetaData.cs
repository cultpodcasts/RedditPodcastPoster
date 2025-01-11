using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs;

public class BBCSoundsMetaData
{
    [JsonPropertyName("programmes")]
    public required Programmes Programmes { get; set; }
}
