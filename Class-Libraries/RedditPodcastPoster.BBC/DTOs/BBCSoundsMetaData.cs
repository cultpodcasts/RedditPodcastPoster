using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs;

public class BBCSoundsMetaData
{
    [JsonPropertyName("props")]
    public required Properties Properties { get; set; }

    [JsonPropertyName("page")]
    public required string Page { get; set; }

    [JsonPropertyName("query")]
    public required Query Query { get; set; }
}