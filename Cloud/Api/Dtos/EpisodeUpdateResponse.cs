using System.Text.Json.Serialization;

namespace Api.Dtos;

public class EpisodeUpdateResponse
{
    [JsonPropertyName("tweetDeleted")]
    public bool? TweetDeleted { get; set; }

    [JsonPropertyName("blueskyPostDeleted")]
    public bool? BlueskyPostDeleted { get; set; }

    [JsonPropertyName("searchIndexerState")]
    public SearchIndexerState? SearchIndexerState { get; set; }
}