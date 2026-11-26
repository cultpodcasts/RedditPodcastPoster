using System.Text.Json.Serialization;
using Api.Contracts;

namespace Api.Dtos;

[ExportContract]
public class EpisodeUpdateResponse
{
    [JsonPropertyName("tweetDeleted")]
    public bool? TweetDeleted { get; set; }

    [JsonPropertyName("blueskyPostDeleted")]
    public bool? BlueskyPostDeleted { get; set; }

    [JsonPropertyName("searchIndexerState")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SearchIndexerState? SearchIndexerState { get; set; }
}