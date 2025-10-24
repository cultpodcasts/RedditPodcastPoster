using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs;

public class Query
{
    [JsonPropertyName("programmeId")]
    public required string ProgrammeId { get; set; }
}