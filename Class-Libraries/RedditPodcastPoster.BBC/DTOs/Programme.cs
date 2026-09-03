using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs;

public class Programme
{
    [JsonPropertyName("release")]
    public Release? Release { get; set; }

    [JsonPropertyName("titles")]
    public required Titles Titles { get; set; }

    [JsonPropertyName("synopses")]
    public Synopses? Synopses { get; set; }

    [JsonPropertyName("duration")]
    public Duration? Duration { get; set; }

    [JsonPropertyName("guidance")]
    public Guidance? Guidance { get; set; }

    [JsonPropertyName("container")]
    public Container? Container { get; set; }
}
