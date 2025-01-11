using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs;

public class Programmes
{
    [JsonPropertyName("current")]
    public required Programme CurrentProgramme { get; set; }
}
