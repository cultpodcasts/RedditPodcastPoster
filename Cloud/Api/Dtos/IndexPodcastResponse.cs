using System.Text.Json.Serialization;
using RedditPodcastPoster.Indexing.Models;

namespace Api.Dtos;

public class IndexPodcastResponse
{
    [JsonPropertyName("indexedEpisodes")]
    public IndexedEpisode[]? IndexedEpisodes { get; set; }

    [JsonPropertyName("indexStatus")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required IndexStatus IndexStatus { get; set; }

    [JsonPropertyName("searchIndexerState")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SearchIndexerState SearchIndexerState { get; set; }
}