using System.Text.Json.Serialization;

namespace Api.Dtos;

public class IndexerState
{
    [JsonPropertyName("state")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RedditPodcastPoster.Search.Models.IndexerState State { get; set; }

    [JsonPropertyName("nextRun")]
    public TimeSpan? NextRun { get; set; }

    [JsonPropertyName("lastRan")]
    public TimeSpan? LastRan { get; set; }
}