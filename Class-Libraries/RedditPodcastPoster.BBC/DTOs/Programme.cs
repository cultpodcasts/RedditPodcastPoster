using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs;

public class Programme
{
    [JsonPropertyName("release")]
    public required Release Release { get; set; }

    [JsonPropertyName("titles")]
    public required Titles Titles { get; set; }

    [JsonPropertyName("synopses")]
    public required Synopses Synopses {  get; set; }

    [JsonPropertyName("duration")]
    public required Duration Duration { get; set; }

    [JsonPropertyName("guidance")]
    public required Guidance Guidance { get; set; }
}