using System.Text.Json.Serialization;

namespace RedditPodcastPoster.InternetArchive.Models;

public class Track
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = null!;

    [JsonPropertyName("file")]
    public Uri File { get; set; } = null!;
}