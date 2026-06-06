using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs;

public class Release
{
    [JsonPropertyName("date")]
    public DateTime? Date { get; set; }
}